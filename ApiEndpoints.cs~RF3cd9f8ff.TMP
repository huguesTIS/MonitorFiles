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

        // repuere un job par son nom
        app.MapPost("/getJob/{name}", (string name, JobConfigurationService configService) =>
        {
            var job = configService.GetConfiguration().Jobs.FirstOrDefault(f => f.Name == name);
            return job is not null ? Results.Ok(job) : Results.NotFound("Folder configuration not found.");
        });
    }
}

