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
    const int maxChunkSize = 4 * 1024 * 1024;

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

        long fileSize = new FileInfo(sourcePath).Length;
        await fileClient.CreateAsync(fileSize);

        using FileStream stream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, maxChunkSize, useAsync: true);
        long offset = 0;
        byte[] buffer = new byte[maxChunkSize];
        int bytesRead;

        while((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            using MemoryStream memoryStream = new MemoryStream(buffer, 0, bytesRead);
            await fileClient.UploadRangeAsync(new HttpRange(offset, bytesRead), memoryStream);
            offset += bytesRead;
        }
         _logger.LogInformation($"Uploaded file of size {fileSize} to {azurePath}");
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

        ShareFileProperties properties = await fileClient.GetPropertiesAsync();
        long fileSize = properties.ContentLength;

        using FileStream stream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, maxChunkSize, useAsync: true);
        long offset = 0;

        while (offset < fileSize)
        {
            long rangeSize = Math.Min(maxChunkSize, fileSize - offset);
            var options = new ShareFileDownloadOptions
            {
                Range = new HttpRange(offset, rangeSize)
            };
            ShareFileDownloadInfo downloadInfo = await fileClient.DownloadAsync(options);
            
            await downloadInfo.Content.CopyToAsync(stream);
            offset += rangeSize;
        }
         _logger.LogInformation($"Downloaded file of size {fileSize} from {azurePath} to {destinationPath}");
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
