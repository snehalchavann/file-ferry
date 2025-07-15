using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using FileFerry.Interfaces;
using FileFerry.Models;
using FileFerry.Services;
using FileFerry.Configurations;
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
string fileHandlerType = builder.Configuration["HandlerType"] ?? "Local";
if (fileHandlerType.Equals("Cloud", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IFileHandler, CloudFileHandler>();
}
else
{
    builder.Services.AddSingleton<IFileHandler, LocalFileHandler>();
}
builder.Services.Configure<AzureSettings>(builder.Configuration.GetSection("Azure"));

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

string CombineCloudPath(string baseUrl, string fileName) => baseUrl.TrimEnd('/') + "/" + fileName;

var localFiles = Directory.GetFiles(sourcePath);
var fileCommands = new List<FileCommand>();

Func<string, string> getArchivePath = fileName => 
fileHandler is CloudFileHandler
    ? CombineCloudPath(archivePath, fileName)
    : Path.Combine(archivePath, fileName);

Func<string, string> getDestinationPath = fileName =>
fileHandler is CloudFileHandler
    ? CombineCloudPath(destinationPath, fileName)
    : Path.Combine(destinationPath, fileName);

// Prepare file commands
foreach (string file in localFiles)
{
    string fileName = Path.GetFileName(file);
    string archiveFilePath = getArchivePath(fileName);
    string destinationFilePath = getDestinationPath(fileName);

    fileCommands.Add(new FileCommand(file, archiveFilePath, FileOperation.Copy));
    fileCommands.Add(new FileCommand(archiveFilePath, destinationFilePath, FileOperation.Move));
    fileCommands.Add(new FileCommand(file, null, FileOperation.Delete));
}

HashSet<string> processedFiles = new HashSet<string>();

// Execute file commands
foreach (FileCommand command in fileCommands)
{
    String destination = command.Operation == FileOperation.Delete ? "N/A" : command.DestinationPath;
    try
    {
        string fileName = Path.GetFileName(command.SourcePath);
        if(command.Operation == FileOperation.Delete)
        {  
            if (!processedFiles.Contains(fileName))
            {
                logger.LogWarning($"Skipping delete: file is not moved: {command.SourcePath}");
                continue;
            }
        }
        await fileHandler.ExecuteCommandAsync(command);

        if(command.Operation == FileOperation.Move)
        {
            processedFiles.Add(fileName);
        }
        logger.LogInformation($"Executed command: {command.Operation} from {command.SourcePath} to {destination}");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, $"Failed to execute command: {command.Operation} from {command.SourcePath} to {destination}");
    }
}