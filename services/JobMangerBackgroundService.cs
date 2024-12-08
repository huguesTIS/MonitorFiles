using Watch2sftp.Core.Monitor;

namespace Watch2sftp.Core.services;

public class JobManagerBackgroundService : BackgroundService, IDisposable
{
    private readonly JobConfigurationService _configService;
    private readonly PathValidatorService _pathValidator;
    private readonly ILogger<JobManagerBackgroundService> _logger;
    private readonly IMonitorFactory _monitorFactory;
    private readonly FileSystemHandlerFactory _fileSystemHandlerFactory;

    // Utilisation d'un dictionnaire thread-safe pour stocker les tokens et Monitors
    private readonly ConcurrentDictionary<string, (CancellationTokenSource TokenSource, IMonitor Monitor)> _jobMonitors = new();

    public JobManagerBackgroundService(
        JobConfigurationService configService,
        PathValidatorService pathValidator,
        ILogger<JobManagerBackgroundService> logger,
        IMonitorFactory monitorFactory,
        FileSystemHandlerFactory fileSystemHandlerFactory)
    {
        _configService = configService;
        _pathValidator = pathValidator;
        _logger = logger;
        _monitorFactory = monitorFactory;
        _fileSystemHandlerFactory = fileSystemHandlerFactory;
    }

    /// <summary>
    /// Méthode principale exécutée par le service Windows.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("JobManager service started.");
        try
        {
            // Charge la configuration et démarre les jobs
            await _configService.LoadConfigAsync(stoppingToken);
            await StartAllJobsAsync(stoppingToken);

            // Boucle principale pour surveiller les mises à jour ou reconnections
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

                // Reconnexion et reprise des jobs
                await CheckAndReconnectJobsAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("JobManager service stopping due to cancellation.");
        }
        finally
        {
            StopAllJobs();
        }
    }

    /// <summary>
    /// Démarre tous les jobs valides depuis la configuration.
    /// </summary>
    public async Task StartAllJobsAsync(CancellationToken stoppingToken)
    {
        var jobs = _configService.GetConfiguration().Jobs.Where(j => j.Enabled);

        foreach (var job in jobs)
        {
            await StartJobAsync(job, stoppingToken);
        }
    }

    /// <summary>
    /// Arrête tous les jobs et libère les ressources.
    /// </summary>
    public void StopAllJobs()
    {
        foreach (var key in _jobMonitors.Keys)
        {
            StopJob(key);
        }
    }

    /// <summary>
    /// Redémarre tous les jobs.
    /// </summary>
    public async Task RestartAllJobsAsync(CancellationToken stoppingToken)
    {
        StopAllJobs();
        await StartAllJobsAsync(stoppingToken);
    }

    /// <summary>
    /// Démarre un job spécifique.
    /// </summary>
    private async Task StartJobAsync(Job job, CancellationToken stoppingToken)
    {
        if (_jobMonitors.ContainsKey(job.Name))
        {
            _logger.LogWarning($"Job {job.Name} is already running.");
            return;
        }

        // Valide les chemins source et destination de manière asynchrone
        if (!await _pathValidator.ValidateSourceAsync(ConnectionStringParser.Parse(job.Source.Path), stoppingToken) ||
            !await _pathValidator.ValidateDestinationAsync(ConnectionStringParser.Parse(job.Destination.Path), stoppingToken))
        {
            _logger.LogError($"Job {job.Name} has invalid paths. Skipping.");
            return;
        }

        // Exécute le Pre-Job avant de démarrer le Monitor
        var preJobManager = new PreJobManager(_fileSystemHandlerFactory, _logger);
        bool preJobSuccess = await preJobManager.ExecutePreJobAsync(job, stoppingToken);

        if (!preJobSuccess)
        {
            _logger.LogError($"Pre-Job for {job.Name} failed. Skipping job start.");
            return;
        }

        // Crée un Monitor pour le job
        var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var monitor = _monitorFactory.CreateMonitor(job);

        // Stocke le Monitor et le token
        if (_jobMonitors.TryAdd(job.Name, (cts, monitor)))
        {
            _logger.LogInformation($"Starting job: {job.Name}");
            _ = Task.Run(() => monitor.StartAsync(cts.Token), cts.Token);
        }
    }

    /// <summary>
    /// Arrête un job spécifique.
    /// </summary>
    private void StopJob(string jobName)
    {
        if (_jobMonitors.TryRemove(jobName, out var jobInfo))
        {
            _logger.LogInformation($"Stopping job: {jobName}");
            jobInfo.TokenSource.Cancel();
            jobInfo.Monitor.Dispose();
        }
    }

    /// <summary>
    /// Vérifie les jobs pour détecter les déconnexions et tente de les reconnecter.
    /// </summary>
    private async Task CheckAndReconnectJobsAsync(CancellationToken stoppingToken)
    {
        foreach (var jobName in _jobMonitors.Keys)
        {
            if (_jobMonitors.TryGetValue(jobName, out var jobInfo))
            {
                if (!await jobInfo.Monitor.IsConnectedAsync())
                {
                    _logger.LogWarning($"Job {jobName} lost connection. Attempting to reconnect...");
                    StopJob(jobName);

                    // Recherche le job dans la configuration
                    var job = _configService.GetConfiguration().Jobs.FirstOrDefault(j => j.Name == jobName);
                    if (job != null)
                    {
                        await StartJobAsync(job, stoppingToken);
                    }
                }
            }
        }
    }

    public override void Dispose()
    {
        StopAllJobs();
        base.Dispose();
    }
}
