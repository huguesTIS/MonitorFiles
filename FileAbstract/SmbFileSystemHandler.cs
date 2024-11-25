namespace Watch2sftp.Core.FileAbstract;
public class SmbFileSystemHandler : IFileSystemHandler

{
    private readonly NetworkCredential _credentials;
    private readonly string _uncPath;

    public SmbFileSystemHandler(ParsedConnectionInfo connectionInfo)
    {
        _credentials = new NetworkCredential(connectionInfo.Username, connectionInfo.Password);
        _uncPath = connectionInfo.Path;
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

    private async Task RunImpersonatedAsync(Func<Task> action)
    {
        await RunImpersonatedAsync(async () =>
        {
            await action();
            return true; // Dummy return for void methods
        });
    }

    public async Task DeleteAsync(string path, CancellationToken cancellationToken)
    {
        await RunImpersonatedAsync(async () =>
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        });
    }

    public async Task<bool> ExistsAsync(string path, CancellationToken cancellationToken)
    {
        return await RunImpersonatedAsync(async () =>
        {
            return File.Exists(path);
        });
    }

    public async Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken)
    {
        return await RunImpersonatedAsync(async () =>
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"File not found: {path}");
            }

            return File.OpenRead(path);
        });
    }

    public async Task WriteAsync(string path, Stream data, CancellationToken cancellationToken)
    {
        await RunImpersonatedAsync(async () =>
        {
            using var fileStream = File.Create(path);
            await data.CopyToAsync(fileStream, cancellationToken);
        });
    }

    public bool IsFileLocked(string path)
    {
        return RunImpersonatedAsync(async () =>
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
        }).GetAwaiter().GetResult();
    }

    public async Task<IEnumerable<FileMetadata>> ListFolderAsync(string path, CancellationToken cancellationToken)
    {
        return await RunImpersonatedAsync(async () =>
        {
            var metadataList = new List<FileMetadata>();

            if (!Directory.Exists(path))
            {
                throw new DirectoryNotFoundException($"Directory not found: {path}");
            }

            var files = Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories);
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

            return metadataList;
        });
    }
}
