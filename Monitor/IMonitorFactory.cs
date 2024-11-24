using Watch2sftp.Core.Model;

namespace Watch2sftp.Core.Monitor;

public interface IMonitorFactory
{
    IMonitor CreateMonitor(Job job, ILogger logger);
}