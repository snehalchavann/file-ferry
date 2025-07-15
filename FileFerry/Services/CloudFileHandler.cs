using Azure.Storage.Files.Shares;
using Azure;
using FileFerry.Interfaces;
using FileFerry.Configurations;
using FileFerry.Models;
using Microsoft.Extensions.Logging;
using Azure.Storage.Files.Shares.Models;
using Microsoft.Extensions.Options;

namespace FileFerry.Services;

public class CloudFileHandler : IFileHandler
{
    private readonly ILogger<CloudFileHandler> _logger;
    private readonly AzureSettings _azureSettings;

    public CloudFileHandler(ILogger<CloudFileHandler> logger, IOptions<AzureSettings> azureSettings)
    {
        _logger = logger;
        _azureSettings = azureSettings.Value;
    }

    public async Task ExecuteCommandAsync(FileCommand command)
    {
        try
        {
            switch (command.Operation)
            {
                case FileOperation.Copy:
                    await UploadFileToCloudAsync(command.SourcePath, command.DestinationPath!);
                    _logger.LogInformation($"Copying from {command.SourcePath} to {command.DestinationPath}");
                    break;
                case FileOperation.Move:
                    string tempFile = Path.GetTempFileName();

                    if(IsCloudPath(command.SourcePath))
                    {
                        await DownloadFileFromCloudAsync(command.SourcePath, tempFile);
                    }
                    else
                    {
                        File.Copy(command.SourcePath, tempFile, true);
                    }
                    await UploadFileToCloudAsync(tempFile, command.DestinationPath!);

                    if(IsCloudPath(command.SourcePath))
                    {
                        await DeleteFileFromCloudAsync(command.SourcePath);
                    }
                    else
                    {
                        File.Delete(command.SourcePath);
                    }

                    File.Delete(tempFile);
                    _logger.LogInformation($"Moving from {command.SourcePath} to {command.DestinationPath}");
                    break;
                case FileOperation.Delete:
                    File.Delete(command.SourcePath);
                    _logger.LogInformation($"Deleting file at {command.SourcePath}");
                    break;
                default:
                    _logger.LogError("Invalid operation.");
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing file command");
            throw;
        }
    }

    private async Task UploadFileToCloudAsync(string sourcePath, string azurePath)
    {
        var (shareName, directoryPath, fileName) = ParseAzurePath(azurePath);
        ShareClient shareClient = new ShareClient(_azureSettings.ConnectionString, shareName);
        ShareDirectoryClient directoryClient = string.IsNullOrEmpty(directoryPath)
         ? shareClient.GetRootDirectoryClient()
         : shareClient.GetDirectoryClient(directoryPath);
         if(!string.IsNullOrEmpty(directoryPath))
         {
             await directoryClient.CreateIfNotExistsAsync();
         }

        ShareFileClient fileClient = directoryClient.GetFileClient(fileName);
        using FileStream stream = File.OpenRead(sourcePath);
        await fileClient.CreateAsync(stream.Length);
        await fileClient.UploadRangeAsync(new HttpRange(0, stream.Length), stream);
    }

    private async Task DeleteFileFromCloudAsync(string azurePath)
    {
        var (shareName, directoryPath, fileName) = ParseAzurePath(azurePath);
        ShareFileClient fileClient = new ShareClient(_azureSettings.ConnectionString, shareName)
                                    .GetDirectoryClient(directoryPath)
                                    .GetFileClient(fileName);
        await fileClient.DeleteIfExistsAsync();
    }

    private async Task DownloadFileFromCloudAsync(string azurePath, string destinationPath)
    {
        var (shareName, directoryPath, fileName) = ParseAzurePath(azurePath);
        ShareFileClient fileClient = new ShareClient(_azureSettings.ConnectionString, shareName)
                                    .GetDirectoryClient(directoryPath)
                                    .GetFileClient(fileName);
        ShareFileDownloadInfo downloadInfo = await fileClient.DownloadAsync();
        using FileStream stream = File.OpenWrite(destinationPath);
        await downloadInfo.Content.CopyToAsync(stream);
    }

    private (string shareName, string directoryPath, string fileName) ParseAzurePath(string azurePath)
    {
            if (!Uri.TryCreate(azurePath, UriKind.Absolute, out var uri))
        throw new ArgumentException("Invalid Azure URL");

    var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

    if (segments.Length < 2)
        throw new ArgumentException("Azure URL must include at least share and file name");

    string shareName = segments[0];
    string filePath = string.Join("/", segments.Skip(1)); 
    string directoryPath = Path.GetDirectoryName(filePath)?.Replace('\\', '/') ?? string.Empty;
    string fileName = Path.GetFileName(filePath);
        _logger.LogDebug($"Parsed Azure path: Share={shareName}, Dir={directoryPath}, File={fileName}");

        return (shareName, directoryPath, fileName);
    }

    private bool IsCloudPath(string path){
        return Uri.TryCreate(path, UriKind.Absolute, out var uri) && 
               uri.Host.Contains(".file.core.windows.net");
    }
}
