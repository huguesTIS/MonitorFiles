namespace Watch2sftp.Core.Model;

public enum MonitorMode
{
    Move,      // Déplacer les fichiers
    Copy,      // Copier les fichiers
    Sync,      // Synchroniser les dossiers (supprime aussi dans la destination
    Archive    // Archiver une copie
}