namespace Watch2sftp.Core.Model;

public class FileEvent
{
    public string FilePath { get; }
    public DateTime EventTime { get; }
    public string EventType { get; }
    public Job JobConfiguration { get; }
    public int RetryCount { get; set; } // Nouveau compteur de retries

    public TimeSpan RetryDelay { get; set; } // Délai avant de ré-enfiler l'événement en cas d'erreur de traitement

    public FileEvent(string filePath, DateTime eventTime, string eventType, Job jobConfiguration)
    {
        FilePath = filePath;
        EventTime = eventTime;
        EventType = eventType;
        JobConfiguration = jobConfiguration;
        RetryCount = 0; // Initialisé à 0
        RetryDelay = TimeSpan.FromSeconds(5); // Délai initial
    }
}
