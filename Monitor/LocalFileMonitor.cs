namespace Watch2sftp.Core.Monitor;

public class LocalFileMonitor : IMonitor
{
    private FileSystemWatcher _watcher;
    private readonly ILogger _logger;
    private readonly Job job;

    public LocalFileMonitor(Job job, ILogger logger)
    {
        _watcher = new FileSystemWatcher(job.Source.Path)
        {
            EnableRaisingEvents = false,
            IncludeSubdirectories = true
        };
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _watcher.EnableRaisingEvents = true;
        _watcher.Created += OnFileEvent;
        _watcher.Changed += OnFileEvent;

        _logger.LogInformation($"LocalFileWatcher started for path: {_watcher.Path}");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (_watcher != null)
        {
            _watcher.Created -= OnFileEvent;
            _watcher.Changed -= OnFileEvent;

        }
        _logger.LogInformation("LocalFileWatcher disposed.");
        return Task.CompletedTask;
    }
    public Task<bool> IsConnectedAsync()
    {
        // Toujours connecte pour un chemin local
        return Task.FromResult(true);
    }


    public void SetEventQueue(IEventQueue eventQueue)
    {
        _eventQueue = eventQueue;
    }

    private async Task OnFileEvent(object sender, FileSystemEventArgs e)
    {
        _logger.LogInformation($"File event detected: {e.FullPath} ({e.ChangeType})");
        await _eventQueue?.EnqueueAsync(new FileEvent(e.FullPath, DateTime.Now, e.ChangeType.ToString(),job));
    }

    public void Dispose()
    {
        _watcher.Dispose();
        _watcher = null;
    }
}
