namespace Watch2sftp.Core.services;

public class JobConfigurationService
{
    private const string ConfigFilePath = "config.json";
    private readonly PathValidatorService _validator;
    private readonly ILogger<JobConfigurationService> _logger;
    private JobConfiguration _config;

    public JobConfigurationService(PathValidatorService validator, ILogger<JobConfigurationService> logger)
    {
        _validator = validator;
        _logger = logger;
    }

    public JobConfiguration GetConfiguration() => _config;

    public JobConfiguration GetDefaultConfiguration()
    {
        var config = new JobConfiguration
        {
            Jobs = new List<Job>
            {
                new()
                {
                    Name = "Job 1",
                    Description = "Description du job 1",
                    Enabled = true,
                    Mode = MonitorMode.Move,
                    FileFilter = "*.*",
                    Source = new SourcePath
                    {
                        Path = @"file://C:/Users/groshugo/Downloads",
                        Description = "Description du job 1"
                    },
                    Destination = new DestinationPath
                    {
                        Path = @"sftp://127.0.0.1:22@user1:password/share",
                        Description = "Description du job 1",                                              
                    },
                    Options = new JobOptions { RetryCount = 3, InitialDelayMs = 5000 }
                }
            }
        };

        SaveConfiguration(config);
        return config;
    }

    public void SaveConfiguration(JobConfiguration config)
    {
        _config = config;
        File.WriteAllText(ConfigFilePath, JsonSerializer.Serialize(config, AppJsonContext.Default.JobConfiguration));
    }

    public async Task LoadConfigAsync(CancellationToken cancellationToken)
    {
        if (File.Exists(ConfigFilePath))
        {
            _config = JsonSerializer.Deserialize(
                File.ReadAllText(ConfigFilePath),
                AppJsonContext.Default.JobConfiguration
            ) ?? GetDefaultConfiguration();
        }
        else
        {
            _config = GetDefaultConfiguration();
        }

        await ValidateConfigAsync(cancellationToken);
    }

    private async Task ValidateConfigAsync(CancellationToken cancellationToken)
    {
        foreach (var job in _config.Jobs)
        {
            if (!job.Enabled)
                continue;

            var isSourceValid = await _validator.ValidateSourceAsync(job.Source, cancellationToken);
            if (!isSourceValid)
            {
                _logger.LogError($"Invalid source path for job: {job.Name}");
                job.Enabled = false; // Désactiver le job
            }

            var isDestinationValid = await _validator.ValidateDestinationAsync(job.Destination, cancellationToken);
            if (!isDestinationValid)
            {
                _logger.LogError($"Invalid destination path for job: {job.Name}");
                job.Enabled = false; // Désactiver le job
            }
        }
    }
}
