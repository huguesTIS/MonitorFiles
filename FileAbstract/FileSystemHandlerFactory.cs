namespace Watch2sftp.Core.Monitor;

public class FileSystemHandlerFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentDictionary<string, IFileSystemHandler> _handlerCache = new();

    public FileSystemHandlerFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IFileSystemHandler CreateHandler(ParsedConnectionInfo path)
    {
        string cacheKey = $"{path.Protocol}:{path.Host}:{path.Port}:{path.Username}";
        return _handlerCache.GetOrAdd(cacheKey, _ => CreateNewHandler(path));
    }

    private IFileSystemHandler CreateNewHandler(ParsedConnectionInfo path)
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


