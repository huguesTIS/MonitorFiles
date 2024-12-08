
namespace Watch2sftp.Core.Monitor;

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
            return true; // Valeur factice
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

    public async IAsyncEnumerable<FileMetadata> ListFolderAsync(
        string path,
        [EnumeratorCancellation] CancellationToken cancellationToken,
        Func<FileMetadata, bool>? filter = null,
        bool recursive = false)
    {
        // On récupère d'abord tous les fichiers via impersonation
        var files = await RunImpersonatedAsync(() => EnumerateFilesAsync(path, cancellationToken, filter, recursive));

        // Puis on les yield asynchronement
        foreach (var fileMetadata in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return fileMetadata;
            // On peut insérer un Task.Yield() si souhaité
            await Task.Yield();
        }
    }

    // Cette méthode retourne désormais une liste plutôt qu'un IAsyncEnumerable
    private async Task<List<FileMetadata>> EnumerateFilesAsync(
        string path,
        CancellationToken cancellationToken,
        Func<FileMetadata, bool>? filter,
        bool recursive)
    {
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"Directory not found: {path}");
        }

        var result = new List<FileMetadata>();

        // Enumération des fichiers
        var files = Directory.EnumerateFiles(path);
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fileInfo = new FileInfo(file);
            var metadata = new FileMetadata
            {
                Path = file,
                Size = fileInfo.Length,
                LastModified = fileInfo.LastWriteTime,
                Extension = fileInfo.Extension
            };

            if (filter == null || filter(metadata))
            {
                result.Add(metadata);
            }
        }

        // Récursivité si nécessaire
        if (recursive)
        {
            var directories = Directory.EnumerateDirectories(path);
            foreach (var dir in directories)
            {
                var subFiles = await EnumerateFilesAsync(dir, cancellationToken, filter, recursive);
                result.AddRange(subFiles);
            }
        }

        return result;
    }
}






