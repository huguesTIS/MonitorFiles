namespace Watch2sftp.Core.Model;

public class ParsedConnectionInfo
{
    public string Protocol { get; set; } // "file", "smb", "sftp"
    public string Host { get; set; }    // Adresse (ex: "127.0.0.1")
    public string Port { get; set; }    // Port (ex: "22"), null si non spécifié
    public string Username { get; set; } // Nom d'utilisateur, null si non spécifié
    public string Password { get; set; } // Mot de passe, null si non spécifié
    public string Path { get; set; }    // Chemin sur le système de fichiers distant
    
}
