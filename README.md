# file-ferry

**FileFerry** is a .NET 8 console application designed to automate file transfers from a source location to an archive and then to a final destination. It supports both **local/UNC paths** and **Azure File Storage**, with built-in logging and workflow based file operations (Copy → Move → Delete).

##  Configuration

All parameters and paths are defined in `appsettings.json`.

### Sample appsettings.json file

```json
{
  "HandlerType": "Local",
  "Paths": {
    "SourcePath": "C:\\FileFerry\\Source",
    "ArchivePath": "C:\\FileFerry\\Archive",
    "DestinationPath": "C:\\FileFerry\\Destination"
  },
  "Azure": {
    "ConnectionString": "<your_azure_file_storage_connection_string>"
  }
}
```

### HandlerType

- `Local`: Use local/UNC file operations
- `Cloud`: Use Azure File Storage operations
- **Default**: If `HandlerType` is not provided, it defaults to **Local**

---

## Flow of File Ferry

For each file in the source folder:

1. **Copy** to archive
2. **Move** from archive to destination
3. **Delete** the original source file if the above two steps succeed

> Each file operation is tracked using a `FileCommand` workflow object.

---

## Logging

Add Logs folder to the root of your project before running the application. All log files will be stored here.

---

## Running the Application

```bash
cd path/to/FileFerry
dotnet run
```