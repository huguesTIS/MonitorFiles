namespace Watch2sftp.Core.services;

public class WorkerService : BackgroundService
{
    private readonly IEventQueue _eventQueue;
    private readonly ILogger<WorkerService> _logger;
    private readonly FileEventProcessor _fileEventProcessor;
    private const int MaxRetries = 3;
    private const int QueueThreshold = 10; // Seuil pour ajuster les workers
    private int _activeWorkers = 1; // Nombre initial de workers

    public WorkerService(
        IEventQueue eventQueue,
        ILogger<WorkerService> logger,
        FileEventProcessor fileEventProcessor)
    {
        _eventQueue = eventQueue;
        _logger = logger;
        _fileEventProcessor = fileEventProcessor;
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

            // Ajuster dynamiquement le nombre de workers en fonction de la queue
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
        try
        {
            _logger.LogInformation($"Processing event: {fileEvent.FilePath} ({fileEvent.EventType})");

            await _fileEventProcessor.ProcessFileEventAsync(fileEvent, stoppingToken);

            _logger.LogInformation($"Processed event successfully: {fileEvent.FilePath} ({fileEvent.EventType})");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to process event: {fileEvent.FilePath} ({fileEvent.EventType})");

            fileEvent.RetryCount++;

            if (fileEvent.RetryCount < MaxRetries)
            {
                _logger.LogInformation($"Retrying event: {fileEvent.FilePath} ({fileEvent.EventType}), attempt {fileEvent.RetryCount}");
                await Task.Delay(TimeSpan.FromSeconds(5 * fileEvent.RetryCount), stoppingToken); // Delai exponentiel
                await _eventQueue.EnqueueAsync(fileEvent, stoppingToken); // Réenfiler l'événement
            }
            else
            {
                _logger.LogError($"Max retries reached for event: {fileEvent.FilePath} ({fileEvent.EventType}). Discarding event.");
            }
        }
    }
}

