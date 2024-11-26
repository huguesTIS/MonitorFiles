namespace Watch2sftp.Core.Monitor;

public class EventQueue : IEventQueue
{
    private readonly BlockingCollection<FileEvent> _queue = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _delayDictionary = new();
    public int QueueCount => _queue.Count;

    private readonly ILogger<EventQueue> _logger;
    private readonly RetryManager _retryManager;

    public EventQueue(ILogger<EventQueue> logger, RetryManager retryManager)
    {
        _logger = logger;
        _retryManager = retryManager;
    }

    public async Task EnqueueAsync(FileEvent fileEvent, CancellationToken cancellationToken, int delayMs = 0)
    {
        fileEvent.Status = "Enqueued"; // Mettre à jour le statut de l'événement

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
        _retryManager.IncrementRetry(fileEvent);
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
            var fileEvent = await Task.Run(() => _queue.Take(cancellationToken));
            if (fileEvent != null)
            {
                _retryManager.MarkProcessing(fileEvent); // Marquer l'événement comme en cours de traitement
            }
            return fileEvent;
        }
        catch (OperationCanceledException)
        {
            // Handle cancellation
            return null;
        }
    }
}