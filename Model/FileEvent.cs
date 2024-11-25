namespace Watch2sftp.Core.Model;
public class FileEvent
{
    public string FilePath { get; }
    public DateTime EventTime { get; }
    public string EventType { get; }
    public FileProcessingContext Context { get; }

    // Gestion des retries
    public int RetryCount { get; set; } // Nombre de tentatives effectuées (modifié pour être accessible en écriture)

    public int MaxRetries { get; private set; } // Nombre maximum de tentatives
    public int RetryDelayMs { get; set; } // Délai avant la prochaine tentative, en millisecondes (modifié pour être accessible en écriture)

    public FileEvent(string filePath, DateTime eventTime, string eventType, FileProcessingContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        FilePath = filePath;
        EventTime = eventTime;
        EventType = eventType;
        Context = context;
        RetryCount = 0;
        MaxRetries = context.MaxRetries;
        RetryDelayMs = context.InitialDelayMs; // Défini par les options du job
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
        RetryDelayMs = Context.InitialDelayMs;
    }
}