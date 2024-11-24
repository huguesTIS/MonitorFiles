namespace Watch2sftp.Core.FileEventHandeler;

public class FileEventProcessor
{
    private readonly ILogger<FileEventProcessor> _logger;
    private readonly IDictionary<string, IFileEventHandler> _handlers;

    public FileEventProcessor(ILogger<FileEventProcessor> logger, IEnumerable<IFileEventHandler> handlers)
    {
        _logger = logger;
        _handlers = handlers.ToDictionary(
            handler => handler.GetType().Name.Replace("Handler", ""),
            handler => handler
        );
    }

    public async Task ProcessFileEventAsync(FileEvent fileEvent, CancellationToken cancellationToken)
    {
        if (_handlers.TryGetValue(fileEvent.EventType + "Handler", out var handler))
        {
            await handler.HandleAsync(fileEvent, cancellationToken);
        }
        else
        {
            _logger.LogWarning($"Aucun gestionnaire trouvé pour l'événement : {fileEvent.EventType}");
        }
    }
}

