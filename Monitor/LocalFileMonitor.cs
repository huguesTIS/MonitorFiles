namespace Watch2sftp.Core.Monitor;

public class LocalFileMonitor : IMonitor
{
    private FileSystemWatcher _watcher;
    private readonly ILogger _logger;
    private readonly FileProcessingContext _context;
    private IEventQueue _eventQueue;

    public LocalFileMonitor(FileProcessingContext context, IEventQueue eventQueue, ILogger logger)
    {
        _eventQueue = eventQueue ?? throw new ArgumentNullException(nameof(eventQueue));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _watcher = new FileSystemWatcher(_context.Source.Path)
        {
            EnableRaisingEvents = false,
            IncludeSubdirectories = true
        };
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _watcher.EnableRaisingEvents = true;
        _watcher.Filter = _context.FileFilter;
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

        _logger.LogInformation("LocalFileWatcher stopped.");
        return Task.CompletedTask;
    }

    public Task<bool> IsConnectedAsync()
    {
        // Toujours connecté pour un chemin local
        return Task.FromResult(true);
    }

    private void OnFileEvent(object sender, FileSystemEventArgs e)
    {
        _logger.LogInformation($"File event detected: {e.FullPath} ({e.ChangeType})");

        // Appel à une méthode asynchrone pour le traitement
        _ = HandleFileEventAsync(e);
    }

    private async Task HandleFileEventAsync(FileSystemEventArgs e)
    {
        await _eventQueue.EnqueueAsync(new FileEvent(e.FullPath, DateTime.Now, e.ChangeType.ToString(), _context), CancellationToken.None);
    }

    public void SetEventQueue(IEventQueue eventQueue)
    {
        _eventQueue = eventQueue ?? throw new ArgumentNullException(nameof(eventQueue));
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _watcher = null;
    }
}
