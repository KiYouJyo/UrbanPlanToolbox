namespace UrbanPlanToolbox.Services;

public interface IAppDataPathProvider
{
    AppDataPaths Paths { get; }
    void EnsureInfrastructureDirectories();
    string GetToolDataDirectory(string toolId);
    string GetToolDataFilePath(string toolId, string fileName);
    string GetToolBackupDirectory(string toolId);
    string GetToolBackupFilePath(string toolId, string fileName);
}
