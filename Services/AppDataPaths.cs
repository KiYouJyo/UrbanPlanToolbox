namespace UrbanPlanToolbox.Services;

public sealed record AppDataPaths(
    string RootDirectory,
    string SettingsFilePath,
    string DataDirectory,
    string ToolsDirectory,
    string ProjectsDirectory,
    string AttachmentsDirectory,
    string ProjectAttachmentsDirectory,
    string BackupsDirectory,
    string ProjectBackupsDirectory,
    string CacheDirectory,
    string LogsDirectory);
