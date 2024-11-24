namespace Watch2sftp.Core.Model;


public class FileEvent
{
    public string FilePath { get; }
    public DateTime EventTime { get; }
    public string EventType { get; }
    public Job JobConfiguration { get; }

    public FileEvent(string filePath, DateTime eventTime, string eventType, Job jobConfiguration)
    {
        FilePath = filePath;
        EventTime = eventTime;
        EventType = eventType;
        JobConfiguration = jobConfiguration;
    }
}
