namespace Watch2sftp.Core;

public class WatcherManager
{
    private readonly Dictionary<string, WatcherService> _watchers = new();

    public void StartWatcher(MonitoredFolder folderConfig, QueueConfiguration queueConfig)
    {
        if (_watchers.ContainsKey(folderConfig.Path))
        {
            StopWatcher(folderConfig.Path);
        }

        var watcher = new WatcherService(folderConfig, queueConfig);
        _watchers[folderConfig.Path] = watcher;
    }

    public void StopWatcher(string folderPath)
    {
        if (_watchers.TryGetValue(folderPath, out var watcher))
        {
            watcher.Dispose();
            _watchers.Remove(folderPath);
        }
    }

    public IEnumerable<MonitoredFolder> GetActiveWatchers()
    {
        return _watchers.Keys.Select(path => new MonitoredFolder { Path = path });
    }
}

