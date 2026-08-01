namespace UrbanPlanToolbox.Services;

public sealed record AppDataPaths(
    string RootDirectory,
    string SettingsFilePath,
    string DataDirectory,
    string ToolsDirectory,
    string AttachmentsDirectory,
    string BackupsDirectory,
    string CacheDirectory,
    string LogsDirectory);
