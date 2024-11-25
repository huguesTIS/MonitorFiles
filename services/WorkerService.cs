namespace Watch2sftp.Core.services;

public class WorkerService : BackgroundService
{
    private readonly IEventQueue _eventQueue;
    private readonly ILogger<WorkerService> _logger;
    private readonly FileSystemHandlerFactory _fileSystemHandlerFactory;
    private const int MaxRetries = 3;
    private const int QueueThreshold = 10; // Seuil pour ajuster les workers
    private int _activeWorkers = 1; // Nombre initial de workers

    public WorkerService(
        IEventQueue eventQueue,
        ILogger<WorkerService> logger,
        FileSystemHandlerFactory fileSystemHandlerFactory)    
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
        var context = fileEvent.Context;
        context.MarkProcessingStarted();

        try
        {
            var sourceHandler = _fileSystemHandlerFactory.CreateHandler(context.Source);
            var destinationHandler = _fileSystemHandlerFactory.CreateHandler(context.Destination);

            switch (context.Mode)
            {
                case MonitorMode.Move:
                    using (var stream = await sourceHandler.OpenReadAsync(fileEvent.FilePath, stoppingToken))
                    {
                        await destinationHandler.WriteAsync(context.Destination.Path, stream, stoppingToken);
                    }
                    await sourceHandler.DeleteAsync(fileEvent.FilePath, stoppingToken);
                    break;

                case MonitorMode.Copy:
                    using (var stream = await sourceHandler.OpenReadAsync(fileEvent.FilePath, stoppingToken))
                    {
                        await destinationHandler.WriteAsync(context.Destination.Path, stream, stoppingToken);
                    }
                    break;

                    // Ajoutez d'autres modes ici
            }

            context.MarkProcessingCompleted();
            _logger.LogInformation($"Processed event: {fileEvent.FilePath} in {context.GetProcessingDuration()}.");
        }
        catch (Exception ex)
        {
            context.IncrementRetry();

            if (context.RetryCount <= context.MaxRetries)
            {
                _logger.LogWarning($"Retrying event: {fileEvent.FilePath}. Attempt {context.RetryCount}.");
                await _eventQueue.EnqueueAsync(fileEvent, stoppingToken, delayMs: context.RetryCount * 5000); // Délai croissant
            }
            else
            {
                _logger.LogError($"Failed to process event: {fileEvent.FilePath} after {context.RetryCount} retries.");
            }
        }
    }

}

