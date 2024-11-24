namespace Watch2sftp.Core.Monitor;
public interface IMonitor : IDisposable
{
    Task<bool> IsConnectedAsync();
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
   // void SetEventQueue(IEventQueue eventQueue);
}
