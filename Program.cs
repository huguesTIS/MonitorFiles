using Watch2sftp.Core;



var builder = WebApplication.CreateSlimBuilder(args);
// Configure JSON serialization options to use the generated context
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default);
});
// exécution comme service Windows
builder.Host.UseWindowsService();
// Ajout des services nécessaires
builder.Services.AddSingleton<JobConfigurationService> (); // Contient les configurations globales
builder.Services.AddSingleton<PathValidatorService>(); // verifie que les chemins definie dans la config sont valid et accessibles
builder.Services.AddHostedService<JobMangerBackgroundService>(); // Service Windows

var app = builder.Build();
// API Minimal pour gerer les configurations et le jobManager
ApiEndpoints.MapEndpoints(app);

// Lancement de l application
app.Run();


