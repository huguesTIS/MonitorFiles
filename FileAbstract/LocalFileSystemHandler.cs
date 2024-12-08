using Renci.SshNet.Sftp;

namespace Watch2sftp.Core.Monitor;

public class LocalFileSystemHandler : IFileSystemHandler
{
    public async Task DeleteAsync(string path, CancellationToken cancellationToken)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
        await Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(string path, CancellationToken cancellationToken)
    {
        return await Task.FromResult(File.Exists(path));
    }

    public async Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"File not found: {path}");
        }

        return await Task.FromResult(File.OpenRead(path));
    }

    public async Task WriteAsync(string path, Stream data, CancellationToken cancellationToken)
    {
        using var fileStream = File.Create(path);
        await data.CopyToAsync(fileStream, cancellationToken);
    }

    public bool IsFileLocked(string path)
    {
        try
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
    }

    public async IAsyncEnumerable<FileMetadata> ListFolderAsync(
        string path,
        [EnumeratorCancellation] CancellationToken cancellationToken,
        Func<FileMetadata, bool>? filter = null,
        bool recursive = false)
    {
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"Directory not found: {path}");
        }

        // Helper method for recursive enumeration
        async IAsyncEnumerable<FileMetadata> ProcessDirectory(string directoryPath, [EnumeratorCancellation] CancellationToken ct)
        {
            var files = Directory.EnumerateFiles(directoryPath);
            foreach (var file in files)
            {
                ct.ThrowIfCancellationRequested();
                var fileInfo = new FileInfo(file);

                var metadata = new FileMetadata
                {
                    Path = file,
                    Size = fileInfo.Length,
                    LastModified = fileInfo.LastWriteTime,
                    Extension = fileInfo.Extension
                };

                // On applique le filtre sur FileMetadata
                if (filter != null && !filter(metadata))
                {
                    continue;
                }

                yield return metadata;

                await Task.Yield(); // Simulate async operation
            }

            if (recursive)
            {
                var directories = Directory.EnumerateDirectories(directoryPath);
                foreach (var dir in directories)
                {
                    await foreach (var subFile in ProcessDirectory(dir, ct))
                    {
                        yield return subFile;
                    }
                }
            }
        }

        await foreach (var file in ProcessDirectory(path, cancellationToken))
        {
            yield return file;
        }
    }


}
