namespace Watch2sftp.Core.FileEventHandeler;

public class FileDeletedHandler : IFileEventHandler
{
    private readonly ILogger<FileDeletedHandler> _logger;
    private readonly FileSystemHandlerFactory _handlerFactory;

    public FileDeletedHandler(ILogger<FileDeletedHandler> logger, FileSystemHandlerFactory handlerFactory)
    {
        _logger = logger;
        _handlerFactory = handlerFactory;
    }

    public async Task HandleAsync(FileEvent fileEvent, CancellationToken cancellationToken)
    {
        var destinationHandler = _handlerFactory.CreateHandler(fileEvent.JobConfiguration.Destination.Path);
        var destinationPath = Path.Combine(fileEvent.JobConfiguration.Destination.Path, Path.GetFileName(fileEvent.FilePath));

        if (await destinationHandler.ExistsAsync(destinationPath, cancellationToken))
        {
            await destinationHandler.DeleteAsync(destinationPath, cancellationToken);
            _logger.LogInformation($"Fichier {destinationPath} supprimé avec succès.");
        }
        else
        {
            _logger.LogWarning($"Fichier {destinationPath} introuvable dans la destination.");
        }
    }
}

