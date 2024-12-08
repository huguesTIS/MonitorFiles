using Renci.SshNet.Sftp;

namespace Watch2sftp.Core.Monitor;


public interface IFileSystemHandler
{
    Task DeleteAsync(string path, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(string path, CancellationToken cancellationToken);
    Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken);
    Task WriteAsync(string path, Stream data, CancellationToken cancellationToken);
    bool IsFileLocked(string path);
    IAsyncEnumerable<FileMetadata> ListFolderAsync(
        string path,
        CancellationToken cancellationToken,
        Func<FileMetadata, bool>? filter = null,
        bool recursive = false
    );
}


