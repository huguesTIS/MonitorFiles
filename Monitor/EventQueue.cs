namespace Watch2sftp.Core.Monitor;

public class EventQueue : IEventQueue
{
    private readonly BlockingCollection<FileEvent> _queue = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _delayDictionary = new();
    public int QueueCount => _queue.Count;

    private readonly ILogger<EventQueue> _logger;

    public EventQueue(ILogger<EventQueue> logger)
    {
        _logger = logger;
    }

    public async Task EnqueueAsync(FileEvent fileEvent, CancellationToken cancellationToken, int delayMs = 0)
    {
        if (_delayDictionary.TryGetValue(fileEvent.FilePath, out var cts))
        {
            cts.Cancel();
            _delayDictionary.TryRemove(fileEvent.FilePath, out _);
        }

        var newCts = new CancellationTokenSource();
        _delayDictionary[fileEvent.FilePath] = newCts;

        try
        {
            if (delayMs > 0)
            {
                await Task.Delay(delayMs, newCts.Token);
            }

            if (!newCts.Token.IsCancellationRequested)
            {
                _queue.Add(fileEvent);
                _delayDictionary.TryRemove(fileEvent.FilePath, out _);
            }
        }
        catch (TaskCanceledException)
        {
            // Le délai a été annulé, ne rien faire.
        }
    }

    public async Task RequeueAsync(FileEvent fileEvent, CancellationToken cancellationToken)
    {
        fileEvent.IncrementRetry();
        TimeSpan retryDelay = TimeSpan.FromMilliseconds(fileEvent.RetryDelayMs); // Utilisation du délai mis à jour

        _logger.LogInformation($"Re-enqueuing event {fileEvent.FilePath} with delay {retryDelay.TotalSeconds}s (Attempt: {fileEvent.RetryCount})");

        if (_delayDictionary.TryGetValue(fileEvent.FilePath, out var cts))
        {
            cts.Cancel();
            _delayDictionary.TryRemove(fileEvent.FilePath, out _);
        }

        var newCts = new CancellationTokenSource();
        _delayDictionary[fileEvent.FilePath] = newCts;

        try
        {
            await Task.Delay(retryDelay, newCts.Token);

            if (!newCts.Token.IsCancellationRequested)
            {
                _queue.Add(fileEvent);
                _delayDictionary.TryRemove(fileEvent.FilePath, out _);
            }
        }
        catch (TaskCanceledException)
        {
            // Le délai a été annulé, ne rien faire.
        }
    }

    public async Task<FileEvent?> DequeueAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await Task.Run(() => _queue.Take(cancellationToken));
        }
        catch (OperationCanceledException)
        {
            // Handle cancellation
            return null;
        }
    }
}
