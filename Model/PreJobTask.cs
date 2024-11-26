namespace Watch2sftp.Core.Model;

public class PreJobTask
{
    public string SourcePath { get; }
    public string DestinationPath { get; }
    public MonitorMode Mode { get; }

    public PreJobTask(string sourcePath, string destinationPath, MonitorMode mode)
    {
        SourcePath = sourcePath;
        DestinationPath = destinationPath;
        Mode = mode;
    }
}
