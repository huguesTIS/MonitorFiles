namespace Watch2sftp.Core.FileAbstract;

public class SftpFileSystemHandler : IFileSystemHandler
{
    private readonly string _host;
    private readonly string _username;
    private readonly string _password;

    public SftpFileSystemHandler(string host, string username, string password)
    {
        _host = host;
        _username = username;
        _password = password;
    }

    public async Task<bool> FileExistsAsync(string path)
    {
        using var client = new Renci.SshNet.SftpClient(_host, _username, _password);
        client.Connect();
        var exists = client.Exists(path);
        client.Disconnect();
        return exists;
    }

    public async Task<bool> DirectoryExistsAsync(string path)
    {
        using var client = new Renci.SshNet.SftpClient(_host, _username, _password);
        client.Connect();
        var exists = client.Exists(path) && client.GetAttributes(path).IsDirectory;
        client.Disconnect();
        return exists;
    }

    public async Task UploadFileAsync(string source, string destination)
    {
        using var client = new Renci.SshNet.SftpClient(_host, _username, _password);
        client.Connect();
        using var fileStream = File.OpenRead(source);
        client.UploadFile(fileStream, destination, true);
        client.Disconnect();
    }

    public async Task DeleteFileAsync(string path)
    {
        using var client = new Renci.SshNet.SftpClient(_host, _username, _password);
        client.Connect();
        client.DeleteFile(path);
        client.Disconnect();
    }

    public async Task<Stream> OpenFileAsync(string path, FileAccess access)
    {
        using var client = new Renci.SshNet.SftpClient(_host, _username, _password);
        client.Connect();
        var stream = new MemoryStream();
        client.DownloadFile(path, stream);
        client.Disconnect();
        stream.Seek(0, SeekOrigin.Begin);
        return stream;
    }
}
