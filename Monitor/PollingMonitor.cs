namespace Watch2sftp.Core.Monitor;

public class PollingMonitor : IMonitor, IDisposable
{
    private readonly IFileSystemHandler _fileSystemHandler;
    private readonly IEventQueue _eventQueue;
    private readonly TimeSpan _pollInterval;
    private readonly ILogger _logger;
    private readonly FileProcessingContext _context;
    private readonly Dictionary<string, FileMetadata> _previousFiles;
    private CancellationTokenSource? _cancellationTokenSource;

    public PollingMonitor(
        FileProcessingContext context,
        IFileSystemHandler fileSystemHandler,    // On passe le handler directement
        IEventQueue eventQueue,
        TimeSpan pollInterval,
        ILogger logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _fileSystemHandler = fileSystemHandler ?? throw new ArgumentNullException(nameof(fileSystemHandler));
        _eventQueue = eventQueue ?? throw new ArgumentNullException(nameof(eventQueue));
        _pollInterval = pollInterval;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _previousFiles = new Dictionary<string, FileMetadata>();
    }

    public async Task StartAsync(CancellationToken stoppingToken)
    {
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        _logger.LogInformation($"PollingMonitor started for source: {_context.Source.Path}");

        while (!_cancellationTokenSource.Token.IsCancellationRequested)
        {
            try
            {
                await PollAndQueueEventsAsync(_cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while executing polling.");
            }

            await Task.Delay(_pollInterval, _cancellationTokenSource.Token);
        }
    }

    private async Task PollAndQueueEventsAsync(CancellationToken cancellationToken)
    {
        var currentFilesDict = new Dictionary<string, FileMetadata>();

        // On utilise ici le handler injecté, qui peut être de type SFTP, local, SMB, etc.
        await foreach (var file in _fileSystemHandler.ListFolderAsync(_context.Source.Path, cancellationToken))
        {
            currentFilesDict[file.Path] = file;

            if (!_previousFiles.TryGetValue(file.Path, out var previousFile))
            {
                _logger.LogInformation($"New file detected: {file.Path}");
                await _eventQueue.EnqueueAsync(
                    new FileEvent(file.Path, DateTime.Now, WatcherChangeTypes.Created.ToString(), _context),
                    cancellationToken);
            }
            else if (previousFile.LastModified < file.LastModified)
            {
                _logger.LogInformation($"Modified file detected: {file.Path}");
                await _eventQueue.EnqueueAsync(
                    new FileEvent(file.Path, DateTime.Now, WatcherChangeTypes.Changed.ToString(), _context),
                    cancellationToken);
            }
        }

        foreach (var previousFile in _previousFiles.Keys.Except(currentFilesDict.Keys))
        {
            _logger.LogInformation($"Deleted file detected: {previousFile}");
            await _eventQueue.EnqueueAsync(
                new FileEvent(previousFile, DateTime.Now, WatcherChangeTypes.Deleted.ToString(), _context),
                cancellationToken);
        }

        _previousFiles.Clear();
        foreach (var kvp in currentFilesDict)
        {
            _previousFiles[kvp.Key] = kvp.Value;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _cancellationTokenSource?.Cancel();
        _logger.LogInformation($"PollingMonitor stopped for source: {_context.Source.Path}");
        await Task.CompletedTask;
    }

    public Task<bool> IsConnectedAsync()
    {
        // On peut tenter un ExistsAsync sur le répertoire source, par exemple,
        // ou considérer qu'il est connecté tant que le polling ne lève pas d'exception.
        return Task.FromResult(true);
    }

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
    }
}

// Extension pour convertir PreJobTask en FileProcessingContext, par exemple
public static class PreJobTaskExtensions
{
    public static FileProcessingContext ToFileProcessingContext(this PreJobTask task)
    {
        var sourceCon = ConnectionStringParser.Parse(task.SourcePath);
        // Selon la logique métier, vous pouvez remplir ce contexte comme nécessaire
        return new FileProcessingContext(sourceCon, null, MonitorMode.Copy, "*", 3, 1000);
    }
}
