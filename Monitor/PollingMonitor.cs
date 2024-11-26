namespace Watch2sftp.Core.Monitor;

public class PollingMonitor : IMonitor
{
    private readonly FileSystemHandlerFactory _fileSystemHandlerFactory;
    private readonly IEventQueue _eventQueue;
    private readonly PreJobTask _preJobTask;
    private readonly TimeSpan _pollInterval;
    private readonly ILogger _logger;
    private readonly Dictionary<string, FileMetadata> _previousFiles; // Cache légère pour stocker les métadonnées des fichiers

    private CancellationTokenSource? _cancellationTokenSource;

    public PollingMonitor(FileSystemHandlerFactory fileSystemHandlerFactory, IEventQueue eventQueue, PreJobTask preJobTask, TimeSpan pollInterval, ILogger logger)
    {
        _fileSystemHandlerFactory = fileSystemHandlerFactory;
        _eventQueue = eventQueue;
        _preJobTask = preJobTask;
        _pollInterval = pollInterval;
        _logger = logger;
        _previousFiles = new Dictionary<string, FileMetadata>();
    }

    public async Task StartAsync(CancellationToken stoppingToken)
    {
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        _logger.LogInformation($"PollingMonitor started for source: {_preJobTask.SourcePath}");

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
        var sourcecon = ConnectionStringParser.Parse(_preJobTask.SourcePath);
        var sourceHandler = _fileSystemHandlerFactory.CreateHandler(sourcecon);
        var currentFiles = await sourceHandler.ListFolderAsync(sourcecon.Path, cancellationToken);

        var currentFilesDict = currentFiles.ToDictionary(f => f.Path, f => f);

        // Detect new or modified files
        foreach (var currentFile in currentFiles)
        {
            if (!_previousFiles.TryGetValue(currentFile.Path, out var previousFile))
            {
                // Nouveau fichier
                _logger.LogInformation($"New file detected: {currentFile.Path}");
                await _eventQueue.EnqueueAsync(new FileEvent(currentFile.Path, DateTime.Now, "Created", new FileProcessingContext(sourcecon, null, MonitorMode.Copy, "*", 3, 1000)), cancellationToken);
            }
            else if (previousFile.LastModified < currentFile.LastModified)
            {
                // Fichier modifié
                _logger.LogInformation($"Modified file detected: {currentFile.Path}");
                await _eventQueue.EnqueueAsync(new FileEvent(currentFile.Path, DateTime.Now, "Modified", new FileProcessingContext(sourcecon, null, MonitorMode.Copy, "*", 3, 1000)), cancellationToken);
            }
        }

        // Detect deleted files
        foreach (var previousFile in _previousFiles.Keys)
        {
            if (!currentFilesDict.ContainsKey(previousFile))
            {
                // Fichier supprimé
                _logger.LogInformation($"Deleted file detected: {previousFile}");
                await _eventQueue.EnqueueAsync(new FileEvent(previousFile, DateTime.Now, "Deleted", new FileProcessingContext(sourcecon, null, MonitorMode.Copy, "*", 3, 1000)), cancellationToken);
            }
        }

        // Mise à jour de la cache
        _previousFiles.Clear();
        foreach (var currentFile in currentFiles)
        {
            _previousFiles[currentFile.Path] = currentFile;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _cancellationTokenSource?.Cancel();
        _logger.LogInformation($"PollingMonitor stopped for source: {_preJobTask.SourcePath}");
        await Task.CompletedTask;
    }

    public Task<bool> IsConnectedAsync()
    {
        // Polling monitor is always "connected" for a local path scenario
        return Task.FromResult(true);
    }

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
    }
}
