using System.Diagnostics;
using FileFerry.Interfaces;
namespace FileFerry.Models;

public enum FileOperation
{
    Copy,
    Move,
    Delete
}


public class FileCommand(string sourcePath, string? destinationPath, FileOperation operation)
{
    public FileOperation Operation { get; } = operation;
    public string SourcePath { get; } = sourcePath;
    public string? DestinationPath { get; } = destinationPath;

    public async Task Execute(IFileHandler fileHandler)
    {
        if (!IsValid(out var errorMessage))
        {
            throw new InvalidOperationException($"Invalid command: {errorMessage}");
        }

        await fileHandler.ExecuteCommandAsync(this);
    }

    public bool IsValid(out string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(SourcePath))
        {
            errorMessage = "Source path is null or empty.";
            return false;
        }

        if((Operation == FileOperation.Copy || Operation == FileOperation.Move) && string.IsNullOrWhiteSpace(DestinationPath))
        {
            errorMessage = $"Destination path is required for {operation} operation.";
            return false;
        }

        if (Operation == FileOperation.Delete && !string.IsNullOrWhiteSpace(DestinationPath))
        {
            errorMessage = "Destination path should not be provided for delete operation.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }
}