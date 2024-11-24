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
        LoadConfig();
        ValidateConfig();
    }

    public JobConfiguration GetConfiguration() => _config;

    public JobConfiguration GetDefauktConfiguration()
    {
        var config = new JobConfiguration()
        {
            Jobs = [
               new() {
                       Name = "Job 1",
                       Description = "Description du job 1",
                       Enabled = true,
                       Source = new SourcePath() {
                           Path = @"file://C:/Users/groshugo/Downloads",
                           Description="Description du job 1"
                            },
                       Destinations = [
                           new() {
                               Path = @"sftp://127.0.0.1:22@user1:password//server/share",
                               Description = "Description du job 1",
                               Mode = "Push",
                               FileFilter = "*.*"
                               }],
                        Options = new JobOptions() { RetryCount = 3, InitialDelayMs = 5000 }
                     }
           ]
        };
        SaveConfiguration(config);
        return config;
    }

    public void SaveConfiguration(JobConfiguration config)
    {
        _config = config;
        File.WriteAllText(ConfigFilePath, JsonSerializer.Serialize(config, AppJsonContext.Default.JobConfiguration));
    }

    private void LoadConfig()
    {
        if (File.Exists(ConfigFilePath))
        {
            _config = JsonSerializer.Deserialize(
                File.ReadAllText(ConfigFilePath),
                AppJsonContext.Default.JobConfiguration
            ) ?? GetDefauktConfiguration();
        }
        else
        {
            _config = GetDefauktConfiguration();
        }
    }

    private void ValidateConfig()
    {
        foreach (var job in _config.Jobs)
        {
            if (!job.Enabled)
                continue;

            if (!_validator.ValidateSource(job.Source))
            {
                _logger.LogError($"Invalid source path for job: {job.Name}");
                job.Enabled = false; // Desactiver le job
            }

            foreach (var destination in job.Destinations)
            {
                if (!_validator.ValidateDestination(destination))
                {
                    _logger.LogError($"Invalid destination path for job: {job.Name}");
                    job.Enabled = false; // Desactiver le job
                }
            }
        }
    }
}



