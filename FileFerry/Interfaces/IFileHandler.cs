namespace FileFerry.Interfaces;

using FileFerry.Models;

public interface IFileHandler
{
    Task ExecuteCommandAsync(FileCommand command);
}