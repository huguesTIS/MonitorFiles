namespace Watch2sftp.Core;

public class WatcherBackgroundService : BackgroundService
{
    private readonly WatcherManager _watcherManager;
    private readonly IConfiguration _configuration;

    public WatcherBackgroundService(WatcherManager watcherManager, IConfiguration configuration)
    {
        _watcherManager = watcherManager;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Charger les configurations de dossier au démarrage
        var folderConfigs = _configuration.GetSection("Folders").Get<List<MonitoredFolder>>() ?? new List<MonitoredFolder>();
        var queueConfig = _configuration.GetSection("QueueConfig").Get<QueueConfiguration>() ?? new QueueConfiguration();

        foreach (var folderConfig in folderConfigs)
        {
            _watcherManager.StartWatcher(folderConfig, queueConfig);
        }

        // Garder le service actif
        while (!stoppingToken.IsCancellationRequested)
        {
            // Surveillance continue ou logique de maintenance
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        // Arrêter tous les watchers avant d'arrêter le service
        foreach (var folderPath in _watcherManager.GetActiveWatchers().Select(f => f.Path))
        {
            _watcherManager.StopWatcher(folderPath);
        }
        return base.StopAsync(cancellationToken);
    }
}
