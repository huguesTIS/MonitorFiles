using Org.BouncyCastle.Asn1.X509;

namespace Watch2sftp.Core;

public static class ApiEndpoints
{
    public static void MapEndpoints(WebApplication app)
    {
        //recupere les jobs
        app.MapGet("/getjob", (JobConfigurationService configService) =>
        {
            var config = configService.GetConfiguration();
            return Results.Ok(config.Jobs);
        });

        // Démarre un Watcher pour un dossier spécifique
        app.MapPost("/getJob/{name}", (string name, JobConfigurationService configService) =>
        {
            var job = configService.GetConfiguration().Jobs.FirstOrDefault(f => f.Name == name);
            return job is not null ? Results.Ok(job) : Results.NotFound("Folder configuration not found.");
        });

        // Récupère la configuration SMTP
        app.MapGet("/getsmtpconfig", (JobConfigurationService configService) =>
        {
        var config = configService.GetConfiguration(); 
            return Results.Ok(config.EmailConfiguration);

        });


        // Récupère la configuration d'un dossier spécifique
        app.MapGet("/getjob/{name}", (string name, JobConfiguration config) =>
        {
            var folder = config.Jobs.FirstOrDefault(f => f.Name == name);
            return folder is not null ? Results.Ok(folder) : Results.NotFound("Folder configuration not found.");
        });

        // Met à jour la configuration d'un dossier
        app.MapPost("/setfolderconfig", (MonitoredFolder updatedFolder, JobConfigurationService configService) =>
        {
            var config = configService.GetConfiguration();
            var folder = config.MonitoredFolders.FirstOrDefault(f => f.Path == updatedFolder.Path);
            if (folder is null) return Results.NotFound("Folder configuration not found.");

            folder.SMBUsername = updatedFolder.SMBUsername;
            folder.SMBPassword = updatedFolder.SMBPassword;
            folder.SFTPServer = updatedFolder.SFTPServer;
            folder.SFTPPort = updatedFolder.SFTPPort;
            folder.SFTPUsername = updatedFolder.SFTPUsername;
            folder.SFTPPassword = updatedFolder.SFTPPassword;

            configService.SaveConfiguration(config);
            return Results.Ok("Folder configuration updated.");
        });

        // Supprime une configuration de dossier
        app.MapDelete("/deletefolderconfig/{name}", (string name, JobConfigurationService configService, WatcherManager manager) =>
        {
            var config = configService.GetConfiguration();
            var folder = config.MonitoredFolders.FirstOrDefault(f => f.Path == name);
            if (folder is null) return Results.NotFound("Folder configuration not found.");

            config.MonitoredFolders.Remove(folder);
            manager.StopWatcher(name); // Arrête le Watcher si en cours
            configService.SaveConfiguration(config);
            return Results.Ok("Folder configuration deleted.");
        });

        // Ajoute une nouvelle configuration de dossier
        app.MapPost("/addfolderconfig", (MonitoredFolder newFolder, JobConfigurationService configService) =>
        {
            var config = configService.GetConfiguration();
            if (config.MonitoredFolders.Any(f => f.Path == newFolder.Path))
                return Results.Conflict("Folder configuration already exists.");

            config.MonitoredFolders.Add(newFolder);
            configService.SaveConfiguration(config);
            return Results.Ok("Folder configuration added.");
        });

        // Démarre un Watcher pour un dossier spécifique
        app.MapPost("/startfolderwatcher/{name}", (string name, Configuration config, WatcherManager manager) =>
        {
            var folder = config.MonitoredFolders.FirstOrDefault(f => f.Path == name);
            if (folder is null) return Results.NotFound("Folder configuration not found.");

            manager.StartWatcher(folder, config.QueueConfiguration);
            return Results.Ok("Folder watcher started.");
        });

        // Arrête un Watcher pour un dossier spécifique
        app.MapPost("/stopfolderwatcher/{name}", (string name, WatcherManager manager) =>
        {
            manager.StopWatcher(name);
            return Results.Ok("Folder watcher stopped.");
        });

        // Redémarre un Watcher pour un dossier spécifique
        app.MapPost("/restartfolderwatcher/{name}", (string name, Configuration config, WatcherManager manager) =>
        {
            var folder = config.MonitoredFolders.FirstOrDefault(f => f.Path == name);
            if (folder is null) return Results.NotFound("Folder configuration not found.");

            manager.StopWatcher(name);
            manager.StartWatcher(folder, config.QueueConfiguration);
            return Results.Ok("Folder watcher restarted.");
        });
    }
}

