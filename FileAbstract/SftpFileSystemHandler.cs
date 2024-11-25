namespace Watch2sftp.Core.FileAbstract;


public class SftpFileSystemHandler : IFileSystemHandler
{
    private readonly string _host;
    private readonly int _port;
    private readonly string _username;
    private readonly string _password;

    public SftpFileSystemHandler(ParsedConnectionInfo connectionInfo)
    {
        _host = connectionInfo.Host;
        _port = connectionInfo.Port ?? 22;
        _username = connectionInfo.Username;
        _password = connectionInfo.Password;
    }

    private SftpClient GetSftpClient()
    {
        return new SftpClient(_host, _port, _username, _password);
    }

    public async Task DeleteAsync(string path, CancellationToken cancellationToken)
    {
        using var client = GetSftpClient();
        client.Connect();
        if (client.Exists(path))
        {
            client.DeleteFile(path);
        }
        client.Disconnect();
        await Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(string path, CancellationToken cancellationToken)
    {
        using var client = GetSftpClient();
        client.Connect();
        var exists = client.Exists(path);
        client.Disconnect();
        return await Task.FromResult(exists);
    }

    public async Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken)
    {
        using var client = GetSftpClient();
        client.Connect();
        if (!client.Exists(path))
        {
            client.Disconnect();
            throw new FileNotFoundException($"File not found: {path}");
        }

        var stream = new MemoryStream();
        client.DownloadFile(path, stream);
        stream.Position = 0; // Reset stream position
        client.Disconnect();

        return await Task.FromResult(stream);
    }

    public async Task WriteAsync(string path, Stream data, CancellationToken cancellationToken)
    {
        using var client = GetSftpClient();
        client.Connect();

        using var memoryStream = new MemoryStream();
        await data.CopyToAsync(memoryStream, cancellationToken);
        memoryStream.Position = 0; // Reset stream position
        client.UploadFile(memoryStream, path);

        client.Disconnect();
    }

    public bool IsFileLocked(string path)
    {
        // On SFTP, file locking is not typically supported. Always return false.
        return false;
    }

    public async Task<IEnumerable<FileMetadata>> ListFolderAsync(string path, CancellationToken cancellationToken)
    {
        using var client = GetSftpClient();
        client.Connect();
        var files = client.ListDirectory(path);

        var metadataList = files
            .Where(f => !f.IsDirectory)
            .Select(f => new FileMetadata
            {
                Path = f.FullName,
                Size = f.Length,
                LastModified = f.LastWriteTime
            });

        client.Disconnect();
        return await Task.FromResult(metadataList);
    }
}
