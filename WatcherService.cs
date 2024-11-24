namespace Watch2sftp.Core;

public class WatcherService : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private readonly BlockingCollection<QueueItem> _fileQueue = new();
    private readonly MonitoredFolder _folderConfig;
    private readonly QueueConfiguration _queueConfig;
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    public WatcherService(MonitoredFolder folderConfig, QueueConfiguration queueConfig)
    {
        _folderConfig = folderConfig ?? throw new ArgumentNullException(nameof(folderConfig));
        _queueConfig = queueConfig ?? throw new ArgumentNullException(nameof(queueConfig));

        _watcher = new FileSystemWatcher
        {
            Path = folderConfig.Path,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
            IncludeSubdirectories = false
        };

        _watcher.Created += OnFileCreated;

        // Start initial processing for existing files
        InitializeQueueForExistingFiles();

        // Start processing the queue
        Task.Run(() => ProcessQueueAsync(_cancellationTokenSource.Token));

        // Start watching
        StartWatcher();
    }

    private void InitializeQueueForExistingFiles()
    {
        foreach (var file in Directory.GetFiles(_folderConfig.Path))
        {
            _fileQueue.Add(new QueueItem(file, 0, _queueConfig.InitialDelayMs));
        }
    }

    private void StartWatcher()
    {
        if (!string.IsNullOrEmpty(_folderConfig.SMBUsername) && !_folderConfig.IsAnonymousSMB)
        {
            var identity = Impersonate.CreateIdentity(_folderConfig.SMBUsername, _folderConfig.SMBPassword);
            WindowsIdentity.RunImpersonated(identity.AccessToken, () =>
            {
                _watcher.EnableRaisingEvents = true;
            });
        }
        else
        {
            _watcher.EnableRaisingEvents = true;
        }
    }

    private void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        _fileQueue.Add(new QueueItem(e.FullPath, 0, _queueConfig.InitialDelayMs));
    }

    private async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        foreach (var item in _fileQueue.GetConsumingEnumerable(cancellationToken))
        {
            try
            {
                // Apply the initial delay
                await Task.Delay(item.InitialDelayMs, cancellationToken);

                if (await IsFileLockedAsync(item.FilePath))
                {
                    // Retry logic if the file is locked
                    if (item.RetryCount < _queueConfig.MaxRetries)
                    {
                        _fileQueue.Add(new QueueItem(item.FilePath, item.RetryCount + 1, _queueConfig.RetryDelayMs));
                    }
                    else
                    {
                        await HandleErrorAsync(item.FilePath, new IOException("File locked after max retries."));
                    }
                    continue;
                }

                // Push the file to SFTP
                await PushFileToSFTPAsync(item.FilePath);
                await HandleSuccessAsync(item.FilePath);
            }
            catch (Exception ex)
            {
                await HandleErrorAsync(item.FilePath, ex);
            }
        }
    }

    private async Task<bool> IsFileLockedAsync(string filePath)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
                return false;
            }
            catch (IOException)
            {
                return true;
            }
        });
    }

    private async Task PushFileToSFTPAsync(string filePath)
    {
        // Replace with actual SFTP transfer logic
        Console.WriteLine($"Transferring file {filePath} to SFTP server {_folderConfig.SFTPServer}...");
        await Task.Delay(500); // Simulate upload
    }

    private async Task HandleSuccessAsync(string filePath)
    {
        Console.WriteLine($"File {filePath} successfully transferred.");

        if (_folderConfig.EmailOnSuccess)
        {
            await SendEmailAsync(_folderConfig.EmailOnSuccessRecipients, "File Transfer Success",
                $"The file '{filePath}' was successfully transferred.");
        }
    }

    private async Task HandleErrorAsync(string filePath, Exception exception)
    {
        Console.WriteLine($"Error processing file {filePath}: {exception.Message}");

        if (_folderConfig.EmailOnError)
        {
            await SendEmailAsync(_folderConfig.EmailOnErrorRecipients, "File Transfer Error",
                $"An error occurred while processing the file '{filePath}': {exception.Message}");
        }
    }

    private async Task SendEmailAsync(IEnumerable<string> recipients, string subject, string body)
    {
        Console.WriteLine($"Sending email to {string.Join(", ", recipients)}: {subject}");
        await Task.Delay(100); // Simulate email sending
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _watcher.Dispose();
        _fileQueue.Dispose();
        _cancellationTokenSource.Dispose();
    }
}

public class QueueItem
{
    public string FilePath { get; }
    public int RetryCount { get; }
    public int InitialDelayMs { get; }

    public QueueItem(string filePath, int retryCount, int initialDelayMs)
    {
        FilePath = filePath;
        RetryCount = retryCount;
        InitialDelayMs = initialDelayMs;
    }
}
