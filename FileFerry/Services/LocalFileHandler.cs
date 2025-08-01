using FileFerry.Models;
using Microsoft.Extensions.Logging;
using FileFerry.Interfaces;

namespace FileFerry.Services;

public class LocalFileHandler : IFileHandler
{
    private readonly ILogger<LocalFileHandler> _logger;
    public LocalFileHandler(ILogger<LocalFileHandler> logger)
    {
        _logger = logger;
    }
    
    public async Task ExecuteCommandAsync(FileCommand command)
    {
        try{
            switch (command.Operation)
            {
                case FileOperation.Copy:
                    File.Copy(command.SourcePath, command.DestinationPath!, true);
                    _logger.LogInformation($"LocalFileHandler: Copied from {command.SourcePath} to {command.DestinationPath}");
                    break;
                case FileOperation.Move:
                    File.Move(command.SourcePath, command.DestinationPath!, true);
                    _logger.LogInformation($"LocalFileHandler: Moved from {command.SourcePath} to {command.DestinationPath}");
                    break;
                case FileOperation.Delete:
                    File.Delete(command.SourcePath);
                    _logger.LogInformation($"LocalFileHandler: Deleted file at {command.SourcePath}");
                    break;
                default:
                    _logger.LogInformation("LocalFileHandler: Invalid operation.");
                    break;
                
            }
        }
        catch
        {
            throw;
        }
        await Task.CompletedTask; 
    }
}