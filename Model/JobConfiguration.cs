namespace Watch2sftp.Core.Model;

public class JobConfiguration
{
    public List<Job> Jobs { get; set; } = new();
}

public class Job
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string FileFilter { get; set; } = "*.*"; // Par défaut, tous les fichiers

    public MonitorMode Mode { get; set; } = MonitorMode.Move; // e.g., Push, Copy,sync
    public SourcePath Source { get; set; } = new();
    public DestinationPath Destination { get; set; } = new();
    public JobOptions Options { get; set; } = new();
}

public class SourcePath
{
    public string Path { get; set; } = string.Empty; // Normalized URI-style path
    public string Description { get; set; } = string.Empty;
}

public class DestinationPath
{
    public string Path { get; set; } = string.Empty; // Normalized URI-style path
    public string Description { get; set; } = string.Empty;
}

public class JobOptions
{
    public int RetryCount { get; set; } = 3;
    public int InitialDelayMs { get; set; } = 1000; // Milliseconds before retry
}
