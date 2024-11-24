namespace Watch2sftp.Core.FileAbstract;

public class LocalFileSystemHandler : IFileSystemHandler
{
    public Task<bool> FileExistsAsync(string path)
    {
        return Task.FromResult(File.Exists(path));
    }

    public Task<bool> DirectoryExistsAsync(string path)
    {
        return Task.FromResult(Directory.Exists(path));
    }

    public Task UploadFileAsync(string source, string destination)
    {
        File.Copy(source, destination, true);
        return Task.CompletedTask;
    }

    public Task DeleteFileAsync(string path)
    {
        File.Delete(path);
        return Task.CompletedTask;
    }

    public Task<Stream> OpenFileAsync(string path, FileAccess access)
    {
        var stream = new FileStream(path, FileMode.Open, access);
        return Task.FromResult((Stream)stream);
    }
}

