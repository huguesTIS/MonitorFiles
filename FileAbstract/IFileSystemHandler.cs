namespace Watch2sftp.Core.FileAbstract;

public interface IFileSystemHandler
{
    Task<bool> FileExistsAsync(string path);
    Task<bool> DirectoryExistsAsync(string path);
    Task UploadFileAsync(string source, string destination);
    Task DeleteFileAsync(string path);
    Task<Stream> OpenFileAsync(string path, FileAccess access);
}

