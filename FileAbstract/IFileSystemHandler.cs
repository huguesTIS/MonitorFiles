namespace Watch2sftp.Core.FileAbstract;


public interface IFileSystemHandler
{
    Task DeleteAsync(string path, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(string path, CancellationToken cancellationToken);
    Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken);
    Task WriteAsync(string path, Stream data, CancellationToken cancellationToken);
    bool IsFileLocked(string path);
    Task<IEnumerable<FileMetadata>> ListFolderAsync(string path, CancellationToken cancellationToken);
}


