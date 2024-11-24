public class MonitorFactory : IMonitorFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IDictionary<string, Func<Job, IServiceProvider, IMonitor>> _monitorCreators;

    public MonitorFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;

        _monitorCreators = new Dictionary<string, Func<Job, IServiceProvider, IMonitor>>
        {
            { "file", (job, sp) =>
                new LocalFileMonitor(
                    job,
                    sp.GetRequiredService<IEventQueue>(), // Résolution de la file d'événements
                    sp.GetRequiredService<ILogger<LocalFileMonitor>>() // Résolution du logger
                )
            },
            // { "smb", (job, sp) => new SmbMonitor(job) },
            // { "sftp", (job, sp) => new SftpMonitor(job) }
        };
    }

    public IMonitor CreateMonitor(Job job)
    {
        var uri = new Uri(job.Source.Path);

        if (!_monitorCreators.TryGetValue(uri.Scheme, out var creator))
        {
            throw new NotSupportedException($"Protocol '{uri.Scheme}' is not supported.");
        }

        return creator(job, _serviceProvider);
    }
}
