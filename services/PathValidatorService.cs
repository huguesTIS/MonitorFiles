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

    public async Task<bool> ValidateSourceAsync(SourcePath source, CancellationToken cancellationToken)
    {
        if (!IsProtocolSupported(source.Path))
        {
            _logger.LogError($"Protocol not supported for source: {source.Path}");
            return false;
        }

        if (!await IsAccessibleAsync(source.Path, cancellationToken))
        {
            _logger.LogError($"Path not accessible: {source.Path}");
            return false;
        }

        return true;
    }

    public async Task<bool> ValidateDestinationAsync(DestinationPath destination, CancellationToken cancellationToken)
    {
        if (!IsProtocolSupported(destination.Path))
        {
            _logger.LogError($"Protocol not supported for destination: {destination.Path}");
            return false;
        }

        if (!await IsAccessibleAsync(destination.Path, cancellationToken))
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

    private async Task<bool> IsAccessibleAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            var handler = _factory.CreateHandler(path);
            return await handler.ExistsAsync(path, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error checking accessibility for path: {path}");
            return false;
        }
    }
}
