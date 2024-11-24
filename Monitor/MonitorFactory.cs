namespace Watch2sftp.Core.Monitor;

public class MonitorFactory : IMonitorFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IDictionary<string, Func<Job, ILogger, IServiceProvider, IMonitor>> _monitorCreators;

    public MonitorFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;

        _monitorCreators = new Dictionary<string, Func<Job, ILogger, IServiceProvider, IMonitor>>
        {
            { "file", (job, logger, sp) => new LocalFileMonitor(job, logger) },
           // { "smb", (job logger, sp) => new SmbMonitor(source) },
           // { "sftp", (job, logger, sp) => new SftpMonitor(source) }
        };
    }

    public IMonitor CreateMonitor(Job job, ILogger logger)
    {
        var uri = new Uri(job.Source.Path);

        if (!_monitorCreators.TryGetValue(uri.Scheme, out var creator))
        {
            throw new NotSupportedException($"Protocol '{uri.Scheme}' is not supported.");
        }

        return creator(job, logger, _serviceProvider);
    }
}

