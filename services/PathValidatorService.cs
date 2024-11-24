namespace Watch2sftp.Core.services;

public class PathValidatorService
{
    private readonly ILogger<PathValidatorService> _logger;
    private readonly FileSystemHandlerFactory _factory;

    public PathValidatorService(ILogger<PathValidatorService> logger, FileSystemHandlerFactory factory)
    {
        _logger = logger;
        _factory = factory;
    }

    public bool ValidateSource(SourcePath source)
    {
        if (!IsProtocolSupported(source.Path))
        {
            _logger.LogError($"Protocol not supported for source: {source.Path}");
            return false;
        }

        if (!IsAccessible(source.Path))
        {
            _logger.LogError($"Path not accessible: {source.Path}");
            return false;
        }

        return true;
    }

    public bool ValidateDestination(DestinationPath destination)
    {
        if (!IsProtocolSupported(destination.Path))
        {
            _logger.LogError($"Protocol not supported for destination: {destination.Path}");
            return false;
        }

        if (!IsAccessible(destination.Path))
        {
            _logger.LogError($"Path not accessible: {destination.Path}");
            return false;
        }

        return true;
    }

    private bool IsProtocolSupported(string path)
    {
        return path.StartsWith("file://") || path.StartsWith("smb://") || path.StartsWith("sftp://");
    }

    private bool IsAccessible(string path)
    {
        try
        {
            var handler = _factory.CreateHandler(path);
            return handler.DirectoryExistsAsync(path).Result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error checking accessibility for path: {path}");
        }

        return false;
    }
}


