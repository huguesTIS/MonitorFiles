namespace Watch2sftp.Core.services;

public class RetryManager
{
    public void IncrementRetry(FileEvent fileEvent)
    {
        fileEvent.RetryCount++;
        fileEvent.RetryDelayMs *= 2; // Double le délai à chaque tentative (backoff exponentiel)
        fileEvent.Status = "Retrying"; // Mettre à jour le statut lorsque le retry est incrémenté
    }

    public void MarkProcessing(FileEvent fileEvent)
    {
        fileEvent.Status = "Processing";
    }

    public void MarkCompleted(FileEvent fileEvent)
    {
        fileEvent.Status = "Completed";
    }
}
