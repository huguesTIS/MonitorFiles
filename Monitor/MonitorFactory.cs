public class MonitorFactory : IMonitorFactory
{
    private readonly IServiceProvider _serviceProvider;

    public MonitorFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IMonitor CreateMonitor(Job job)
    {
        var sourceInfo = ConnectionStringParser.Parse(job.Source.Path);
        var destinationInfo = ConnectionStringParser.Parse(job.Destination.Path);

        var context = new FileProcessingContext(
            sourceInfo,
            destinationInfo,
            job.Mode,
            job.FileFilter
        );

        return sourceInfo.Protocol switch
        {
            "file" => new LocalFileMonitor(context, _serviceProvider.GetRequiredService<IEventQueue>(), _serviceProvider.GetRequiredService<ILogger<LocalFileMonitor>>()),
            //"smb" => new SmbFileMonitor(context, _serviceProvider.GetRequiredService<IEventQueue>(), _serviceProvider.GetRequiredService<ILogger<SmbFileMonitor>>()),
            //"sftp" => new SftpFileMonitor(context, _serviceProvider.GetRequiredService<IEventQueue>(), _serviceProvider.GetRequiredService<ILogger<SftpFileMonitor>>()),
            _ => throw new NotSupportedException($"Unsupported protocol: {sourceInfo.Protocol}")
        };
    }
}

