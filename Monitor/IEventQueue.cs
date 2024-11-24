namespace Watch2sftp.Core.Monitor;

public interface IEventQueue
{
    Task EnqueueAsync(FileEvent fileEvent, CancellationToken cancellationToken);
    Task<FileEvent?> DequeueAsync(CancellationToken cancellationToken);
}