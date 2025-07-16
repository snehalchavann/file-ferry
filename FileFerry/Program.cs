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
builder.Services.AddSingleton<FileProcessor>();

// Register services
var app = builder.Build();
var logger = app.Services.GetRequiredService<ILogger<Program>>();
var fileHandler = app.Services.GetRequiredService<IFileHandler>();
var config = app.Services.GetRequiredService<IConfiguration>();
var fileProcessor = app.Services.GetRequiredService<FileProcessor>();

// Read file paths from configuration
string? sourcePath = config["Paths:SourcePath"];
string? destinationPath = config["Paths:DestinationPath"];
string? archivePath = config["Paths:ArchivePath"];

if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(destinationPath) || string.IsNullOrWhiteSpace(archivePath))
{
    logger.LogError("Source, Destination, or Archive paths are not configured properly.");
    return;
}

// Process files
await fileProcessor.ProcessFileCommandAsync(sourcePath, archivePath, destinationPath);
