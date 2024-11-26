namespace Watch2sftp.Core.Model;
public class FileEvent
{
    public string FilePath { get; }
    public DateTime EventTime { get; }
    public string EventType { get; }
    public string Status { get; set; } // Ajout d'une propriété Status pour gérer l'état de l'événement
    public FileProcessingContext Context { get; }

    // Gestion des retries
    public int RetryCount { get; set; } // Nombre de tentatives effectuées (modifié pour être accessible en écriture)

    public int MaxRetries { get; private set; } // Nombre maximum de tentatives
    public int RetryDelayMs { get; set; } // Délai avant la prochaine tentative, en millisecondes (modifié pour être accessible en écriture)

    public FileEvent(string filePath, DateTime eventTime, string eventType, FileProcessingContext context)
    {
        FilePath = filePath;
        EventTime = eventTime;
        EventType = eventType;
        Context = context;
        RetryCount = 0;
        MaxRetries = context.MaxRetries;
        RetryDelayMs = context.InitialDelayMs; // Défini par les options du job
        Status = "Enqueued"; // Statut initial
    }

    /// <summary>
    /// Réinitialise les informations de retry.
    /// </summary>
    public void ResetRetry()
    {
        RetryCount = 0;
        RetryDelayMs = Context.InitialDelayMs;
        Status = "Enqueued"; // Réinitialiser le statut
    }
}
