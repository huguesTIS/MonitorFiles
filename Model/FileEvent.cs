namespace Watch2sftp.Core.Model;

public class FileEvent
{
    public string FilePath { get; }
    public DateTime EventTime { get; }
    public string EventType { get; }
    public FileProcessingContext Context { get; }

    public FileEvent(string filePath, DateTime eventTime, string eventType, FileProcessingContext context)
    {
        FilePath = filePath;
        EventTime = eventTime;
        EventType = eventType;
        Context = context;
    }
}

