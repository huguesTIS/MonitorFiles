namespace Watch2sftp.Core.FileAbstract;

public class FileSystemHandlerFactory
{
    public IFileSystemHandler CreateHandler(string path)
    {
        if (path.StartsWith("file://"))
        {
            return new LocalFileSystemHandler();
        }
        else if (path.StartsWith("sftp://"))
        {
            // Extrait les paramètres de connexion depuis le chemin ou une configuration externe
            return new SftpFileSystemHandler("host", "username", "password");
        }
        else if (path.StartsWith("smb://"))
        {
            // Extrait les paramètres de connexion depuis le chemin ou une configuration externe
            return new SmbFileSystemHandler("host",  "username", "password");
        }

        throw new NotSupportedException($"Protocol not supported for path: {path}");
    }
}

