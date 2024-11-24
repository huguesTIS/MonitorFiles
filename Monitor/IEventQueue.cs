namespace Watch2sftp.Core.Monitor;

public interface IEventQueue
{
    int QueueCount { get; }
    Task EnqueueAsync(FileEvent fileEvent, CancellationToken cancellationToken);
    Task<FileEvent?> DequeueAsync(CancellationToken cancellationToken);
}