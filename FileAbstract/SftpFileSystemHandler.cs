using Renci.SshNet;
using Renci.SshNet.Sftp;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

public class SftpFileSystemHandler : IFileSystemHandler
{
    private readonly string _host;
    private readonly int _port;
    private readonly string _username;
    private readonly string _password;

    public SftpFileSystemHandler(string host, int port, string username, string password)
    {
        _host = host;
        _port = port;
        _username = username;
        _password = password;
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
}
