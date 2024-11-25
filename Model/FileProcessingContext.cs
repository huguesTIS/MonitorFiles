namespace Watch2sftp.Core.Model;

public class FileProcessingContext
{
    public ParsedConnectionInfo Source { get; }
    public ParsedConnectionInfo Destination { get; }
    public MonitorMode Mode { get; }
    public string FileFilter { get; }

    // Suivi des métriques
    public DateTime EnqueuedTime { get; private set; } // Temps d'ajout dans la file
    public DateTime? StartProcessingTime { get; private set; } // Temps de début de traitement
    public DateTime? EndProcessingTime { get; private set; } // Temps de fin de traitement
    public int RetryCount { get; private set; } // Nombre de tentatives de traitement

    public FileProcessingContext(ParsedConnectionInfo source, ParsedConnectionInfo destination, MonitorMode mode, string fileFilter)
    {
        Source = source;
        Destination = destination;
        Mode = mode;
        FileFilter = fileFilter;
        EnqueuedTime = DateTime.Now;
        RetryCount = 0;
    }

    public void IncrementRetry()
    {
        RetryCount++;
    }

    public void MarkProcessingStarted()
    {
        StartProcessingTime = DateTime.Now;
    }

    public void MarkProcessingCompleted()
    {
        EndProcessingTime = DateTime.Now;
    }

    public TimeSpan GetProcessingDuration()
    {
        if (StartProcessingTime.HasValue && EndProcessingTime.HasValue)
        {
            return EndProcessingTime.Value - StartProcessingTime.Value;
        }
        return TimeSpan.Zero;
    }

    public TimeSpan GetQueueDuration()
    {
        if (StartProcessingTime.HasValue)
        {
            return StartProcessingTime.Value - EnqueuedTime;
        }
        return DateTime.Now - EnqueuedTime;
    }
}

