namespace Watch2sftp.Core.services;


public class JobMangerBackgroundService : BackgroundService, IDisposable
{
    private readonly JobConfigurationService _configService;
    private readonly PathValidatorService _pathValidator;
    private readonly ILogger<JobMangerBackgroundService> _logger;
    private readonly MonitorFactory _monitorFactory;

    // Utilisation d un dictionnaire thread-safe pour stocker les tokens et Monitors
    private readonly ConcurrentDictionary<string, (CancellationTokenSource TokenSource, IMonitor Monitor)> _jobMonitors = new();

    public JobMangerBackgroundService(
        JobConfigurationService configService,
        PathValidatorService pathValidator,
        ILogger<JobMangerBackgroundService> logger,
        MonitorFactory monitorFactory)
    {
        _configService = configService;
        _pathValidator = pathValidator;
        _logger = logger;
        _monitorFactory = monitorFactory;
    }

    /// <summary>
    /// Methode principale executee par le service Windows
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("JobManager service started.");
        try
        {
            // Verifie et demarre les jobs
            StartAllJobs();

            // Boucle principale pour surveiller les mises a jour ou reconnections
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

                // Reconnexion et reprise des jobs
                await CheckAndReconnectJobs(stoppingToken);
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
    /// Demarre tous les jobs valides depuis la configuration
    /// </summary>
    public void StartAllJobs()
    {
        var jobs = _configService.GetConfiguration().Jobs.Where(j => j.Enabled);

        foreach (var job in jobs)
        {
            StartJob(job);
        }
    }

    /// <summary>
    /// Arrete tous les jobs et libere les ressources
    /// </summary>
    public void StopAllJobs()
    {
        foreach (var key in _jobMonitors.Keys)
        {
            StopJob(key);
        }
    }

    /// <summary>
    /// Redemarre tous les jobs
    /// </summary>
    public void RestartAllJobs()
    {
        StopAllJobs();
        StartAllJobs();
    }

    /// <summary>
    /// Demarre un job specifique
    /// </summary>
    private void StartJob(Job job)
    {
        if (_jobMonitors.ContainsKey(job.Name))
        {
            _logger.LogWarning($"Job {job.Name} is already running.");
            return;
        }

        // Verifie que les chemins source et destination sont valides
        if (!_pathValidator.ValidateSource(job.Source) ||
            job.Destinations.Any(d => !_pathValidator.ValidateDestination(d)))
        {
            _logger.LogError($"Job {job.Name} has invalid paths. Skipping.");
            return;
        }

        // Cree un Monitor pour le job
        var cts = new CancellationTokenSource();
        var Monitor = _monitorFactory.CreateMonitor(job);

        // Stocke le Monitor et le token
        if (_jobMonitors.TryAdd(job.Name, (cts, Monitor)))
        {
            _logger.LogInformation($"Starting job: {job.Name}");
            Task.Run(() => Monitor.StartAsync(cts.Token), cts.Token);
        }
    }

    /// <summary>
    /// Arrete un job specifique
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
    /// Verifie les jobs pour détecter les deconnexions et tenter de les reconnecter
    /// </summary>
    private async Task CheckAndReconnectJobs(CancellationToken stoppingToken)
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
                        StartJob(job);
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

