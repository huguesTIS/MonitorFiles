namespace Watch2sftp.Core.FileAbstract;

public class SmbFileSystemHandler : IFileSystemHandler
{
    private readonly NetworkCredential _credentials;

    public SmbFileSystemHandler(string host, string username, string password, string domain = ".")
    {
        _credentials = new NetworkCredential(username, password, domain);
    }

    public async Task<bool> FileExistsAsync(string path)
    {
        return await RunImpersonatedAsync(async () =>
        {
            return File.Exists(path);
        });
    }

    public async Task UploadFileAsync(string source, string destination)
    {
        await RunImpersonatedAsync(async () =>
        {
            File.Copy(source, destination, overwrite: true);
            return Task.CompletedTask;
        });
    }

    public async Task DeleteFileAsync(string path)
    {
        await RunImpersonatedAsync(async () =>
        {
            File.Delete(path);
            return Task.CompletedTask;
        });
    }

    public async Task<Stream> OpenFileAsync(string path, FileAccess access)
    {
        return await RunImpersonatedAsync(() =>
        {
            var fileStream = new FileStream(path, FileMode.Open, access);
            return Task.FromResult<Stream>(fileStream);
        });
    }

    public async Task<bool> DirectoryExistsAsync(string path)
    {
        return await RunImpersonatedAsync(async () =>
        {
            return Directory.Exists(path);
        });
    }

    private async Task<T> RunImpersonatedAsync<T>(Func<Task<T>> action)
    {
        if (_credentials == null)
        {
            return await action();
        }

        using var identity = Impersonate.CreateIdentity(
            _credentials.UserName,
            _credentials.Password,
            _credentials.Domain);

        return await WindowsIdentity.RunImpersonated(identity.AccessToken, action);
    }
}
