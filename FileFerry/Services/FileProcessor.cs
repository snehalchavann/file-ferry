using FileFerry.Models;
using FileFerry.Interfaces;
using Microsoft.Extensions.Logging;

namespace FileFerry.Services;

public class FileProcessor
{
    private readonly IFileHandler _fileHandler;
    private readonly ILogger<FileProcessor> _logger;

    public FileProcessor(IFileHandler fileHandler, ILogger<FileProcessor> logger)
    {
        _fileHandler = fileHandler;
        _logger = logger;
    }

    public async Task ProcessFileCommandAsync(string sourcePath, string archivePath, string destinationPath, int maxParallelism)
    {
        var localFiles = Directory.GetFiles(sourcePath);
        await Parallel.ForEachAsync(localFiles, new ParallelOptions { MaxDegreeOfParallelism = maxParallelism }, async (file, cancellationToken) =>
        {
            string fileName = Path.GetFileName(file);
            string archiveFilePath = GetPath(archivePath,fileName);
            string destinationFilePath = GetPath(destinationPath,fileName);

            try{
                await _fileHandler.ExecuteCommandAsync(new FileCommand(file, archiveFilePath, FileOperation.Copy));
                _logger.LogInformation($"FileProcessor: Copied {file} to archive at {archiveFilePath}");

                await _fileHandler.ExecuteCommandAsync(new FileCommand(archiveFilePath, destinationFilePath, FileOperation.Move));
                _logger.LogInformation($"FileProcessor: Moved {archiveFilePath} to destination at {destinationFilePath}");

                await _fileHandler.ExecuteCommandAsync(new FileCommand(file, null, FileOperation.Delete));
                _logger.LogInformation($"FileProcessor: Deleted original file at {file}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"FileProcessor: Failed to process file {file}");
            }
        });
    }

    private string GetPath(string basePath, string fileName)
    {
        return _fileHandler is CloudFileHandler
            ? basePath.TrimEnd('/') + "/" + fileName
            : Path.Combine(basePath, fileName);
    }
}