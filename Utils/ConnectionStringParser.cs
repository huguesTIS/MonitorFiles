namespace Watch2sftp.Core.Utils;

public static class ConnectionStringParser
{
    public static ParsedConnectionInfo Parse(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));
        }

        // Utilisation de Uri pour analyser la chaîne de connexion
        var uri = new Uri(connectionString);

        var parsedInfo = new ParsedConnectionInfo
        {
            Protocol = uri.Scheme.ToLower(), // "file", "smb", "sftp"
            Host = NormalizeHost(uri),
            Port = uri.IsDefaultPort ? null : uri.Port,
            Path = NormalizePath(uri),
        };

        // Extraire les informations d'identification si présentes
        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            var userInfoParts = uri.UserInfo.Split(':');
            parsedInfo.Username = userInfoParts[0];
            parsedInfo.Password = userInfoParts.Length > 1 ? userInfoParts[1] : null;
        }

        return parsedInfo;
    }

    /// <summary>
    /// Normalise l'hôte pour les protocoles comme SMB ou SFTP.
    /// </summary>
    private static string NormalizeHost(Uri uri)
    {
        // Pour `file://`, l'hôte est généralement vide
        return uri.Scheme.Equals("file", StringComparison.OrdinalIgnoreCase) ? null : uri.Host;
    }

    /// <summary>
    /// Normalise le chemin en fonction du protocole.
    /// Pour `smb://`, retourne un chemin UNC au format `\\serveur\share`.
    /// </summary>
    private static string NormalizePath(Uri uri)
    {
        var path = uri.AbsolutePath.TrimStart('/'); // Supprime les slashes initiaux

        if (uri.Scheme.Equals("file", StringComparison.OrdinalIgnoreCase))
        {
            // Remplace les slashes par des backslashes pour Windows
            return path.Replace("/", "\\");
        }
        else if (uri.Scheme.Equals("smb", StringComparison.OrdinalIgnoreCase))
        {
            // Formate le chemin SMB en UNC : \\host\share
            return $"\\\\{uri.Host}\\{path.Replace("/", "\\")}";
        }

        // Pour les autres protocoles, retourne le chemin tel quel
        return path;
    }
}