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
builder.Services.AddSingleton<IEventQueue, EventQueue>(); // File d'événements
builder.Services.AddSingleton<IMonitorFactory, MonitorFactory>(); // Usine de monitors
builder.Services.AddSingleton<FileSystemHandlerFactory>();
builder.Services.AddSingleton<JobConfigurationService> (); // Contient les configurations globales
builder.Services.AddSingleton<PathValidatorService>(); // verifie que les chemins definie dans la config sont valid et accessibles
builder.Services.AddHostedService<JobManagerBackgroundService>(); // Service Windows
builder.Services.AddLogging(); // Ajout du logging



var app = builder.Build();
// API Minimal pour gerer les configurations et le jobManager
ApiEndpoints.MapEndpoints(app);

// Lancement de l application
app.Run();


