namespace Watch2sftp.Core.FileAbstract;

public class FileSystemHandlerFactory
{
    private readonly IServiceProvider _serviceProvider;

    public FileSystemHandlerFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IFileSystemHandler CreateHandler(ParsedConnectionInfo path)
    {


        return path.Protocol switch
        {
            "file" => _serviceProvider.GetRequiredService<LocalFileSystemHandler>(),
            "smb" => new SmbFileSystemHandler(path),
            "sftp" => new SftpFileSystemHandler(path),

            _ => throw new NotSupportedException($"Unsupported protocol: {path.Protocol}")
        };
    }
}


