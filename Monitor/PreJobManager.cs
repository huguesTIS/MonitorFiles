
using FastRsync.Core;
using FastRsync.Delta;
using FastRsync.Diagnostics;
using FastRsync.Signature;
using System.Buffers;
using System.IO.Compression;
using System.Text;

namespace Watch2sftp.Core.Monitor;



public class PreJobManager
{
    private readonly FileSystemHandlerFactory _fileSystemHandlerFactory;
    private readonly ILogger _logger;
    private const int MaxRetries = 3;
    private const long LargeFileThreshold = 10 * 1024 * 1024; // 10 MB threshold for using FastRsync
    private const int MaxConcurrency = 4;
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(MaxConcurrency);
    private readonly ConcurrentBag<MemoryStream> _memoryStreamPool = new();

    public PreJobManager(FileSystemHandlerFactory fileSystemHandlerFactory, ILogger logger)
    {
        _fileSystemHandlerFactory = fileSystemHandlerFactory;
        _logger = logger;
    }

    // Executes a pre-job task, handling different modes such as Move, Copy, Sync, or Archive.
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
                case MonitorMode.Sync:
                case MonitorMode.Archive:
                    await CopyAsync(preJobTask.SourcePath, preJobTask.DestinationPath, preJobTask.Mode == MonitorMode.Sync, preJobTask.Mode == MonitorMode.Archive, cancellationToken);
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

    // Moves files from the source to the destination.
    private async Task MoveAsync(string source, string destination, CancellationToken cancellationToken)
    {
        await ProcessFilesInBatchAsync(source, destination, async (file, sourceHandler, destinationHandler, destinationFilePath) =>
        {
            using var stream = await sourceHandler.OpenReadAsync(file.Path, cancellationToken);
            await destinationHandler.WriteAsync(destinationFilePath, stream, cancellationToken);
            await sourceHandler.DeleteAsync(file.Path, cancellationToken);
        }, cancellationToken);
    }

    // Copies files from the source to the destination, optionally synchronizing or archiving.
    private async Task CopyAsync(string source, string destination, bool sync, bool archiveFiles, CancellationToken cancellationToken)
    {
        var sourcecon = ConnectionStringParser.Parse(source);
        var destinationcon = ConnectionStringParser.Parse(destination);

        var sourceHandler = _fileSystemHandlerFactory.CreateHandler(sourcecon);
        var destinationHandler = _fileSystemHandlerFactory.CreateHandler(destinationcon);

        StringBuilder archiveFolderBuilder = new StringBuilder();
        if (archiveFiles)
        {
            archiveFolderBuilder.Append(Path.Combine(destinationcon.Path, "@Archives", DateTime.Now.ToString("yyyy"), DateTime.Now.ToString("MM")));
            Directory.CreateDirectory(archiveFolderBuilder.ToString());
        }

        // Retrieve the destination files once and create a dictionary for quick lookup
        // var destinationFiles = await destinationHandler.ListFolderAsync(destinationcon.Path, cancellationToken).ToListAsync(cancellationToken);
        // var destinationFilesDict = destinationFiles.ToDictionary(f => f.Path, f => f);
        // Dictionnaire pour un accès rapide aux fichiers de destination
        var destinationFilesDict = new Dictionary<string, FileMetadata>();
        await foreach (var destFile in destinationHandler.ListFolderAsync(destinationcon.Path, cancellationToken))
        {
            destinationFilesDict[destFile.Path] = destFile;
        }

        using var archiveStream = archiveFiles ? new FileStream(Path.Combine(archiveFolderBuilder.ToString(), $"archive_{DateTime.Now:yyyyMMddHHmmss}.zip"), FileMode.Create) : Stream.Null;
        using var archive = archiveFiles ? new ZipArchive(archiveStream, ZipArchiveMode.Create) : null;

        await foreach (var file in sourceHandler.ListFolderAsync(sourcecon.Path, cancellationToken))
        {
            var destinationFilePath = Path.Combine(destinationcon.Path, file.Path);

            if (!destinationFilesDict.TryGetValue(file.Path, out var destFile) || destFile.LastModified < file.LastModified)
            {
                _logger.LogInformation($"Copying file {file.Path} to destination.");
                using var stream = await sourceHandler.OpenReadAsync(file.Path, cancellationToken);
                var buffer = ArrayPool<byte>.Shared.Rent(81920);
                MemoryStream memoryStream = GetMemoryStream();
                try
                {
                    int bytesRead;
                    while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
                    {
                        memoryStream.SetLength(0);
                        memoryStream.Write(buffer, 0, bytesRead);
                        memoryStream.Seek(0, SeekOrigin.Begin);
                        await destinationHandler.WriteAsync(destinationFilePath, memoryStream, cancellationToken);
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                    ReturnMemoryStream(memoryStream);
                }

                if (archiveFiles && archive != null)
                {
                    var entry = archive.CreateEntry(file.Path, CompressionLevel.Optimal);
                    using var entryStream = entry.Open();
                    stream.Position = 0;
                    await stream.CopyToAsync(entryStream, cancellationToken);
                }
            }
            else if (destFile.LastModified < file.LastModified && file.Size > LargeFileThreshold)
            {
                _logger.LogInformation($"Using FastRsync to sync large updated file {file.Path}.");
                await FastRsyncAsync(file.Path, destinationFilePath, sourceHandler, destinationHandler, cancellationToken);

                if (archiveFiles && archive != null)
                {
                    var entry = archive.CreateEntry(file.Path, CompressionLevel.Optimal);
                    using var entryStream = entry.Open();
                    using var updatedStream = await sourceHandler.OpenReadAsync(file.Path, cancellationToken);
                    await updatedStream.CopyToAsync(entryStream, cancellationToken);
                }
            }
        }

        if (sync)
        {
            // Remove files from destination that are no longer in source
            var sourceFilesSet = new HashSet<string>();
            await foreach (var srcFile in sourceHandler.ListFolderAsync(sourcecon.Path, cancellationToken))
            {
                sourceFilesSet.Add(srcFile.Path);
            }
            foreach (var destFile in destinationFilesDict.Values)
            {
                if (!sourceFilesSet.Contains(destFile.Path))
                {
                    _logger.LogInformation($"Removing file {destFile.Path} from destination as it no longer exists in source.");
                    await destinationHandler.DeleteAsync(destFile.Path, cancellationToken);
                }
            }
        }
    }

    // Synchronizes large updated files using FastRsync.
    private async Task FastRsyncAsync(string sourcePath, string destinationPath, IFileSystemHandler sourceHandler, IFileSystemHandler destinationHandler, CancellationToken cancellationToken)
    {
        using var signatureStream = GetMemoryStream();
        using (var sourceStream = await sourceHandler.OpenReadAsync(sourcePath, cancellationToken))
        {
            var signatureBuilder = new  SignatureBuilder();
            signatureBuilder.Build(sourceStream, new SignatureWriter(signatureStream));
        }

        using var deltaStream = GetMemoryStream();
        using (var destinationStream = await destinationHandler.OpenReadAsync(destinationPath, cancellationToken))
        {
            var progressHandler = new Progress<ProgressReport>(); // Gestionnaire de progression par défaut
            var deltaBuilder = new DeltaBuilder();
            deltaBuilder.BuildDelta(destinationStream, new SignatureReader(signatureStream, progressHandler), new AggregateCopyOperationsDecorator(new BinaryDeltaWriter(deltaStream)));
        }


        deltaStream.Seek(0, SeekOrigin.Begin);
        await destinationHandler.WriteAsync(destinationPath, deltaStream, cancellationToken);
    }

    // Processes files in batch with a specified file action.
    private async Task ProcessFilesInBatchAsync(string source, string destination, Func<FileMetadata, IFileSystemHandler, IFileSystemHandler, string, Task> fileAction, CancellationToken cancellationToken)
    {
        var sourcecon = ConnectionStringParser.Parse(source);
        var destinationcon = ConnectionStringParser.Parse(destination);

        var sourceHandler = _fileSystemHandlerFactory.CreateHandler(sourcecon);
        var destinationHandler = _fileSystemHandlerFactory.CreateHandler(destinationcon);

        await foreach (var file in sourceHandler.ListFolderAsync(sourcecon.Path, cancellationToken))
        {
            var tasks = new List<Task>();
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                var destinationFilePath = Path.Combine(destinationcon.Path, file.Path);
                tasks.Add(fileAction(file, sourceHandler, destinationHandler, destinationFilePath));
            }
            finally
            {
                _semaphore.Release();
            }

            await Task.WhenAll(tasks);
        }
    }

    // Processes files in batch using a specified action.
    private async Task ProcessFilesInBatchAsync(IEnumerable<FileMetadata> files, Func<FileMetadata, Task> action, int maxConcurrency, CancellationToken cancellationToken)
    {
        var tasks = files.Select(async file =>
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                await action(file);
            }
            finally
            {
                _semaphore.Release();
            }
        }).ToList();

        await Task.WhenAll(tasks);
    }

    // Retries an action with exponential backoff in case of failure.
    private async Task RetryWithBackoffAsync(Func<Task> action, int maxRetries, CancellationToken cancellationToken)
    {
        int retryCount = 0;
        var delay = TimeSpan.FromSeconds(1);

        while (retryCount < maxRetries)
        {
            try
            {
                await action();
                return; // Success, exit the loop
            }
            catch (Exception ex)
            {
                retryCount++;
                if (retryCount >= maxRetries)
                    throw; // Too many retries

                _logger.LogWarning($"Retrying due to exception: {ex.Message}. Retry count: {retryCount}.");
                await Task.Delay(delay, cancellationToken);
                delay *= 2; // Exponential backoff
            }
        }
    }

    // Gets a MemoryStream from the pool or creates a new one if none are available.
    private MemoryStream GetMemoryStream()
    {
        return _memoryStreamPool.TryTake(out var stream) ? stream : new MemoryStream();
    }

    // Returns a MemoryStream to the pool for reuse.
    private void ReturnMemoryStream(MemoryStream stream)
    {
        stream.SetLength(0);
        _memoryStreamPool.Add(stream);
    }
}
