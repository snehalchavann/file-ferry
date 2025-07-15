using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using FileFerry.Interfaces;
using FileFerry.Models;
using FileFerry.Services;
using Serilog;
using Serilog.Events;

var builder = Host.CreateApplicationBuilder(args);

// Configure logging
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
.AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true);
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog();

// Register file handlers
builder.Services.AddSingleton<IFileHandler, LocalFileHandler>();

// Register services
var app = builder.Build();
var logger = app.Services.GetRequiredService<ILogger<Program>>();
var fileHandler = app.Services.GetRequiredService<IFileHandler>();
var config = app.Services.GetRequiredService<IConfiguration>();

// Read file paths from configuration
string? sourcePath = config["Paths:SourcePath"];
string? destinationPath = config["Paths:DestinationPath"];
string? archivePath = config["Paths:ArchivePath"];

if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(destinationPath) || string.IsNullOrWhiteSpace(archivePath))
{
    logger.LogError("Source, Destination, or Archive paths are not configured properly.");
    return;
}

// Create a file command
var fileCommands = new List<FileCommand>
{
    new FileCommand(Path.Combine(sourcePath, "testfile.txt"), Path.Combine(archivePath, "testfile.txt"), FileOperation.Copy),
    new FileCommand(Path.Combine(archivePath, "testfile.txt"), Path.Combine(destinationPath, "testfile.txt"), FileOperation.Move),
    new FileCommand(Path.Combine(sourcePath, "testfile.txt"), null, FileOperation.Delete)
};

// Execute file commands
foreach (FileCommand command in fileCommands)
{
    String destination = command.Operation == FileOperation.Delete ? "N/A" : command.DestinationPath;
    try
    {
        await fileHandler.ExecuteCommandAsync(command);
        logger.LogInformation($"Executed command: {command.Operation} from {command.SourcePath} to {destination}");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, $"Failed to execute command: {command.Operation} from {command.SourcePath} to {destination}");
    }
}