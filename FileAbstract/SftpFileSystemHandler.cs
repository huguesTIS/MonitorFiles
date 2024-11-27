namespace Watch2sftp.Core.Monitor;

public class SftpFileSystemHandler : IFileSystemHandler
{
    private readonly string _host;
    private readonly int _port;
    private readonly string _username;
    private readonly string _password;
    private SftpClient _client;

    public SftpFileSystemHandler(ParsedConnectionInfo connectionInfo)
    {
        _host = connectionInfo.Host;
        _port = connectionInfo.Port ?? 22;
        _username = connectionInfo.Username;
        _password = connectionInfo.Password;

        // Establish a single connection to be reused
        _client = new SftpClient(_host, _port, _username, _password);
        _client.Connect();
    }

    private void EnsureConnected()
    {
        if (!_client.IsConnected)
        {
            _client.Connect();
        }
    }

    public async Task DeleteAsync(string path, CancellationToken cancellationToken)
    {
        EnsureConnected();
        if (_client.Exists(path))
        {
            _client.DeleteFile(path);
        }
        await Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(string path, CancellationToken cancellationToken)
    {
        EnsureConnected();
        var exists = _client.Exists(path);
        return await Task.FromResult(exists);
    }

    public async Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken)
    {
        EnsureConnected();
        if (!_client.Exists(path))
        {
            throw new FileNotFoundException($"File not found: {path}");
        }

        var stream = new MemoryStream();
        _client.DownloadFile(path, stream);
        stream.Position = 0; // Reset stream position

        return await Task.FromResult(stream);
    }

    public async Task WriteAsync(string path, Stream data, CancellationToken cancellationToken)
    {
        EnsureConnected();

        using var memoryStream = new MemoryStream();
        await data.CopyToAsync(memoryStream, cancellationToken);
        memoryStream.Position = 0; // Reset stream position
        _client.UploadFile(memoryStream, path);
    }

    public bool IsFileLocked(string path)
    {
        // On SFTP, file locking is not typically supported. Always return false.
        return false;
    }

    public async IAsyncEnumerable<FileMetadata> ListFolderAsync(string path, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        EnsureConnected();
        var files = _client.ListDirectory(path);

        foreach (var file in files.Where(f => !f.IsDirectory))
        {
            yield return new FileMetadata
            {
                Path = file.FullName,
                Size = file.Length,
                LastModified = file.LastWriteTime
            };

            await Task.Yield(); // Yield to avoid blocking
        }
    }

    public void Dispose()
    {
        if (_client != null)
        {
            _client.Disconnect();
            _client.Dispose();
            _client = null;
        }
    }
}
