namespace Watch2sftp.Core.Monitor;

    public class EventQueue : IEventQueue
    {
        private readonly BlockingCollection<FileEvent> _queue = new();
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _delayDictionary = new();
        public int QueueCount => _queue.Count;

    public async Task EnqueueAsync(FileEvent fileEvent, CancellationToken cancellationToken)
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
            await Task.Delay(TimeSpan.FromSeconds(5), newCts.Token);

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

    public async Task<FileEvent> DequeueAsync(CancellationToken cancellationToken)
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
