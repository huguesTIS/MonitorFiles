namespace Watch2sftp.Core.services;

public class WorkerService : BackgroundService
{
    private readonly IEventQueue _eventQueue;
    private readonly ILogger<WorkerService> _logger;
    private readonly FileSystemHandlerFactory _fileSystemHandlerFactory;
    private readonly ConcurrentDictionary<string, int> _retryCounts = new();
    private const int MaxRetries = 3;
    private const int QueueThreshold = 10; // Seuil pour lancer des workers supplementaires
    private int _activeWorkers = 1; // Nombre initial de workers

    public WorkerService(IEventQueue eventQueue, ILogger<WorkerService> logger, FileSystemHandlerFactory fileSystemHandlerFactory)
    {
        _eventQueue = eventQueue;
        _logger = logger;
        _fileSystemHandlerFactory = fileSystemHandlerFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WorkerService started.");

        var workerTasks = new List<Task>();

        for (int i = 0; i < _activeWorkers; i++)
        {
            workerTasks.Add(ProcessEventsAsync(stoppingToken));
        }

        await Task.WhenAll(workerTasks);

        _logger.LogInformation("WorkerService stopping.");
    }

    private async Task ProcessEventsAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var fileEvent = await _eventQueue.DequeueAsync(stoppingToken);
            if (fileEvent != null)
            {
                await ProcessEventAsync(fileEvent, stoppingToken);
            }

            // Verifier la taille de la queue et ajuster le nombre de workers
            if (_eventQueue.QueueCount > QueueThreshold && _activeWorkers < Environment.ProcessorCount)
            {
                _activeWorkers++;
                _logger.LogInformation($"Increasing worker count to {_activeWorkers}");
                _ = ProcessEventsAsync(stoppingToken); // Lancer un nouveau worker
            }
        }
    }

    private async Task ProcessEventAsync(FileEvent fileEvent, CancellationToken stoppingToken)
    {
        _logger.LogInformation($"Processing event: {fileEvent.FilePath} ({fileEvent.EventType})");

        try
        {
            var handler = _fileSystemHandlerFactory.CreateHandler(fileEvent.JobConfiguration.Source.Path);
            await handler.UploadFileAsync(fileEvent.FilePath, fileEvent.JobConfiguration.Destinations[0].Path);
            _logger.LogInformation($"Processed event: {fileEvent.FilePath} ({fileEvent.EventType})");
            _retryCounts.TryRemove(fileEvent.FilePath, out _);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to process event: {fileEvent.FilePath} ({fileEvent.EventType})");

            // Mecanisme de retry
            if (_retryCounts.TryGetValue(fileEvent.FilePath, out var retryCount))
            {
                if (retryCount < MaxRetries)
                {
                    _retryCounts[fileEvent.FilePath] = retryCount + 1;
                    _logger.LogInformation($"Retrying event: {fileEvent.FilePath} ({fileEvent.EventType}), attempt {retryCount + 1}");
                    await Task.Delay(TimeSpan.FromSeconds(5 * retryCount), stoppingToken); // Delai avant de réessayer
                    await _eventQueue.EnqueueAsync(fileEvent, stoppingToken); // Reenfiler l evenement
                }
                else
                {
                    _logger.LogError($"Max retries reached for event: {fileEvent.FilePath} ({fileEvent.EventType})");
                    _retryCounts.TryRemove(fileEvent.FilePath, out _);
                }
            }
            else
            {
                _retryCounts[fileEvent.FilePath] = 1;
                _logger.LogInformation($"Retrying event: {fileEvent.FilePath} ({fileEvent.EventType}), attempt 1");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); // Delai avant de reessayer
                await _eventQueue.EnqueueAsync(fileEvent, stoppingToken); // Reenfiler l'evenement
            }
        }
    }
}
