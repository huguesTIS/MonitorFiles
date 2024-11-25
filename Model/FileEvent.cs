namespace Watch2sftp.Core.Model;

public class FileEvent
{
    public string FilePath { get; }
    public DateTime EventTime { get; }
    public string EventType { get; }
    public FileProcessingContext Context { get; }

    // Gestion des retries
    public int RetryCount { get; private set; } // Nombre de tentatives effectuées

    public int MaxRetries { get; private set; } // Nombre maximum de tentatives
    public int RetryDelayMs { get; private set; } // Délai avant la prochaine tentative, en millisecondes

    public FileEvent(string filePath, DateTime eventTime, string eventType, FileProcessingContext context)
    {
        FilePath = filePath;
        EventTime = eventTime;
        EventType = eventType;
        Context = context;
        RetryCount = 0;
        RetryDelayMs = context.Source.Options.InitialDelayMs; // Défini par les options du job
    }

    /// <summary>
    /// Augmente le compteur de retries et le délai associé.
    /// </summary>
    public void IncrementRetry()
    {
        RetryCount++;
        RetryDelayMs *= 2; // Double le délai à chaque tentative (backoff exponentiel)
    }

    /// <summary>
    /// Réinitialise les informations de retry.
    /// </summary>
    public void ResetRetry()
    {
        RetryCount = 0;
        RetryDelayMs = Context.Source.Options.InitialDelayMs;
    }
}


