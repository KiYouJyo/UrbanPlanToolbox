using UrbanPlanToolbox.Models.Tools;

namespace UrbanPlanToolbox.Services;

public sealed class AppDataPathProvider : IAppDataPathProvider
{
    private readonly HashSet<string> _registeredToolIds;

    public static AppDataPathProvider Default { get; } = new(
        GetDefaultRootDirectory(),
        ToolRegistry.Default.All.Select(tool => tool.Id));

    private static string GetDefaultRootDirectory()
    {
#if DEBUG
        var isolatedTestRoot = Environment.GetEnvironmentVariable("URBANPLANTOOLBOX_TEST_DATA_ROOT");
        if (!string.IsNullOrWhiteSpace(isolatedTestRoot)) return Path.GetFullPath(isolatedTestRoot);
#endif
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UrbanPlanToolbox");
    }

    public AppDataPathProvider(string rootDirectory, IEnumerable<string> registeredToolIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        ArgumentNullException.ThrowIfNull(registeredToolIds);

        var root = Path.GetFullPath(rootDirectory);
        _registeredToolIds = new HashSet<string>(registeredToolIds, StringComparer.Ordinal);
        if (_registeredToolIds.Any(id => !IsSafeSegment(id)))
        {
            throw new ArgumentException("Registered tool IDs must be safe path segments.", nameof(registeredToolIds));
        }

        var data = Path.Combine(root, "data");
        Paths = new AppDataPaths(
            root,
            Path.Combine(root, "settings.json"),
            data,
            Path.Combine(data, "tools"),
            Path.Combine(data, "projects"),
            Path.Combine(root, "attachments"),
            Path.Combine(root, "attachments", "projects"),
            Path.Combine(root, "backups"),
            Path.Combine(root, "backups", "projects"),
            Path.Combine(root, "cache"),
            Path.Combine(root, "logs"));
    }

    public AppDataPaths Paths { get; }

    public void EnsureInfrastructureDirectories()
    {
        foreach (var path in new[]
                 {
                     Paths.RootDirectory, Paths.DataDirectory, Paths.ToolsDirectory, Paths.ProjectsDirectory,
                     Paths.AttachmentsDirectory, Paths.ProjectAttachmentsDirectory, Paths.BackupsDirectory,
                     Paths.ProjectBackupsDirectory,
                     Paths.CacheDirectory, Paths.LogsDirectory
                 })
        {
            Directory.CreateDirectory(path);
        }
    }

    public string GetToolDataDirectory(string toolId)
    {
        ValidateRegisteredToolId(toolId);
        var path = Path.Combine(Paths.ToolsDirectory, toolId);
        Directory.CreateDirectory(path);
        return path;
    }

    public string GetToolDataFilePath(string toolId, string fileName) =>
        GetSafeFilePath(GetToolDataDirectory(toolId), fileName, requireJsonExtension: true);

    public string GetToolBackupDirectory(string toolId)
    {
        ValidateRegisteredToolId(toolId);
        var path = Path.Combine(Paths.BackupsDirectory, toolId);
        Directory.CreateDirectory(path);
        return path;
    }

    public string GetToolBackupFilePath(string toolId, string fileName) =>
        GetSafeFilePath(GetToolBackupDirectory(toolId), $"{fileName}.last-valid.bak", requireJsonExtension: false);

    public string GetProjectsIndexFilePath() =>
        GetSafeFilePath(Paths.ProjectsDirectory, "index.json", requireJsonExtension: true);

    public string GetProjectsIndexBackupFilePath() =>
        GetSafeFilePath(Paths.ProjectBackupsDirectory, "index.json.last-valid.bak", requireJsonExtension: false);

    public string GetProjectDataDirectory(Guid projectId) =>
        EnsureProjectDirectory(Paths.ProjectsDirectory, projectId);

    public string GetProjectDataFilePath(Guid projectId) =>
        GetSafeFilePath(GetProjectDataDirectory(projectId), "project.json", requireJsonExtension: true);

    public string GetProjectBackupDirectory(Guid projectId) =>
        EnsureProjectDirectory(Paths.ProjectBackupsDirectory, projectId);

    public string GetProjectBackupFilePath(Guid projectId) =>
        GetSafeFilePath(GetProjectBackupDirectory(projectId), "project.json.last-valid.bak", requireJsonExtension: false);

    public string GetProjectAttachmentsDirectory(Guid projectId) =>
        EnsureProjectDirectory(Paths.ProjectAttachmentsDirectory, projectId);

    public string GetPreImportBackupDirectory()
    {
        var path = Path.Combine(Paths.BackupsDirectory, "pre-import");
        Directory.CreateDirectory(path);
        return path;
    }

    private static string EnsureProjectDirectory(string root, Guid projectId)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("Project ID cannot be empty.", nameof(projectId));
        }

        var path = Path.Combine(root, projectId.ToString("D"));
        Directory.CreateDirectory(path);
        return path;
    }

    private void ValidateRegisteredToolId(string toolId)
    {
        if (!IsSafeSegment(toolId) || !_registeredToolIds.Contains(toolId))
        {
            throw new ArgumentException("Tool ID is invalid or is not registered.", nameof(toolId));
        }
    }

    private static string GetSafeFilePath(string directory, string fileName, bool requireJsonExtension)
    {
        if (!IsSafeSegment(fileName) ||
            requireJsonExtension && !string.Equals(Path.GetExtension(fileName), ".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Data file names must be simple .json file names.", nameof(fileName));
        }

        var fullDirectory = Path.GetFullPath(directory) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(directory, fileName));
        if (!fullPath.StartsWith(fullDirectory, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The data path escapes its tool directory.", nameof(fileName));
        }

        return fullPath;
    }

    private static bool IsSafeSegment(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        !Path.IsPathRooted(value) &&
        value is not "." and not ".." &&
        value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
        !value.Contains(Path.DirectorySeparatorChar) &&
        !value.Contains(Path.AltDirectorySeparatorChar);
}
