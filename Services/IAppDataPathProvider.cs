namespace UrbanPlanToolbox.Services;

public interface IAppDataPathProvider
{
    AppDataPaths Paths { get; }
    void EnsureInfrastructureDirectories();
    string GetToolDataDirectory(string toolId);
    string GetToolDataFilePath(string toolId, string fileName);
    string GetToolBackupDirectory(string toolId);
    string GetToolBackupFilePath(string toolId, string fileName);
    string GetToolAttachmentsDirectory(string toolId);
    string GetToolAttachmentDirectory(string toolId, string parentId);
    string GetProjectsIndexFilePath();
    string GetProjectsIndexBackupFilePath();
    string GetProjectDataDirectory(Guid projectId);
    string GetProjectDataFilePath(Guid projectId);
    string GetProjectBackupDirectory(Guid projectId);
    string GetProjectBackupFilePath(Guid projectId);
    string GetProjectAttachmentsDirectory(Guid projectId);
    string GetPreImportBackupDirectory();
}
