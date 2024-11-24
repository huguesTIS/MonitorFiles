namespace Watch2sftp.Core.FileEventHandeler;

public interface IFileEventHandler
{
    Task HandleAsync(FileEvent fileEvent, CancellationToken cancellationToken);
}
