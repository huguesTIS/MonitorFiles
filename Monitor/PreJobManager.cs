using Watch2sftp.Core.Model;

namespace Watch2sftp.Core.Monitor;

public class PreJobManager
{
    private readonly FileSystemHandlerFactory _fileSystemHandlerFactory;
    private readonly ILogger _logger;

    public PreJobManager(FileSystemHandlerFactory fileSystemHandlerFactory, ILogger logger)
    {
        _fileSystemHandlerFactory = fileSystemHandlerFactory;
        _logger = logger;
    }

    public async Task<bool> ExecutePreJobAsync(PreJobTask preJobTask, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation($"Starting PreJobTask: {preJobTask.Mode} from {preJobTask.SourcePath} to {preJobTask.DestinationPath}");

            switch (preJobTask.Mode)
            {
                case MonitorMode.Move:
                    await MoveAsync(preJobTask.SourcePath, preJobTask.DestinationPath, cancellationToken);
                    break;
                case MonitorMode.Copy:
                    await CopyAsync(preJobTask.SourcePath, preJobTask.DestinationPath, cancellationToken);
                    break;
                case MonitorMode.Sync:
                    await SyncAsync(preJobTask.SourcePath, preJobTask.DestinationPath, cancellationToken);
                    break;
                default:
                    throw new NotSupportedException($"Unsupported mode: {preJobTask.Mode}");
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to execute PreJobTask from {preJobTask.SourcePath} to {preJobTask.DestinationPath}");
            return false;
        }
    }

    private async Task MoveAsync(string source, string destination, CancellationToken cancellationToken)
    {
        var sourcecon = ConnectionStringParser.Parse(source);
        var sourceHandler = _fileSystemHandlerFactory.CreateHandler(sourcecon);
        var destinationcon = ConnectionStringParser.Parse(destination);
        var destinationHandler = _fileSystemHandlerFactory.CreateHandler(destinationcon);

        var files = await sourceHandler.ListFolderAsync(sourcecon.Path, cancellationToken);
        foreach (var file in files)
        {
            var destinationFilePath = Path.Combine(destinationcon.Path, file.Path);
            if (await destinationHandler.ExistsAsync(destinationFilePath, cancellationToken))
            {
                _logger.LogWarning($"File {file.Path} already exists in destination. Skipping move.");
                continue;
            }

            using var stream = await sourceHandler.OpenReadAsync(file.Path, cancellationToken);
            await destinationHandler.WriteAsync(destinationFilePath, stream, cancellationToken);
            await sourceHandler.DeleteAsync(file.Path, cancellationToken);
        }
    }

    private async Task CopyAsync(string source, string destination, CancellationToken cancellationToken)
    {
        var sourcecon = ConnectionStringParser.Parse(source);
        var sourceHandler = _fileSystemHandlerFactory.CreateHandler(sourcecon);
        var destinationcon = ConnectionStringParser.Parse(destination);
        var destinationHandler = _fileSystemHandlerFactory.CreateHandler(destinationcon);

        var files = await sourceHandler.ListFolderAsync(sourcecon.Path, cancellationToken);
        foreach (var file in files)
        {
            var destinationFilePath = Path.Combine(destinationcon.Path, file.Path);
            if (await destinationHandler.ExistsAsync(destinationFilePath, cancellationToken))
            {
                _logger.LogWarning($"File {file.Path} already exists in destination. Skipping copy.");
                continue;
            }

            using var stream = await sourceHandler.OpenReadAsync(file.Path, cancellationToken);
            await destinationHandler.WriteAsync(destinationFilePath, stream, cancellationToken);
        }
    }

    private async Task SyncAsync(string source, string destination, CancellationToken cancellationToken)
    {
        var sourcecon = ConnectionStringParser.Parse(source);
        var sourceHandler = _fileSystemHandlerFactory.CreateHandler(sourcecon);
        var destinationcon = ConnectionStringParser.Parse(destination);
        var destinationHandler = _fileSystemHandlerFactory.CreateHandler(destinationcon);

        var sourceFiles = await sourceHandler.ListFolderAsync(sourcecon.Path, cancellationToken);
        var destinationFiles = await destinationHandler.ListFolderAsync(destinationcon.Path, cancellationToken);

        var destinationFilesDict = destinationFiles.ToDictionary(f => f.Path, f => f);

        // Copy new or updated files from source to destination
        foreach (var file in sourceFiles)
        {
            var destinationFilePath = Path.Combine(destinationcon.Path, file.Path);
            if (!destinationFilesDict.TryGetValue(file.Path, out var destFile) || destFile.LastModified < file.LastModified)
            {
                _logger.LogInformation($"Syncing file {file.Path} to destination.");
                using var stream = await sourceHandler.OpenReadAsync(file.Path, cancellationToken);
                await destinationHandler.WriteAsync(destinationFilePath, stream, cancellationToken);
            }
        }

        // Remove files from destination that are no longer in source
        var sourceFilesSet = new HashSet<string>(sourceFiles.Select(f => f.Path));
        foreach (var destFile in destinationFiles)
        {
            if (!sourceFilesSet.Contains(destFile.Path))
            {
                _logger.LogInformation($"Removing file {destFile.Path} from destination as it no longer exists in source.");
                await destinationHandler.DeleteAsync(destFile.Path, cancellationToken);
            }
        }
    }
}
