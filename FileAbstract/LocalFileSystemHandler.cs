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

    public async Task<IEnumerable<FileMetadata>> ListFolderAsync(string path, CancellationToken cancellationToken)
    {
        var files = Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories);
        var metadataList = new List<FileMetadata>();

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var info = new FileInfo(file);
            metadataList.Add(new FileMetadata
            {
                Path = file,
                Size = info.Length,
                LastModified = info.LastWriteTime
            });
        }

        return await Task.FromResult(metadataList);
    }
}


