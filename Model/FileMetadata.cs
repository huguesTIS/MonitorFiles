namespace Watch2sftp.Core.Model;

public class FileMetadata
{
    public string Path { get; set; }
    public long Size { get; set; }
    public DateTime LastModified { get; set; }
}
