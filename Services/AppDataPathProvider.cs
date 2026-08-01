using UrbanPlanToolbox.Models.Tools;

namespace UrbanPlanToolbox.Services;

public sealed class AppDataPathProvider : IAppDataPathProvider
{
    private readonly HashSet<string> _registeredToolIds;

    public static AppDataPathProvider Default { get; } = new(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UrbanPlanToolbox"),
        ToolRegistry.Default.All.Select(tool => tool.Id));

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
            Path.Combine(root, "attachments"),
            Path.Combine(root, "backups"),
            Path.Combine(root, "cache"),
            Path.Combine(root, "logs"));
    }

    public AppDataPaths Paths { get; }

    public void EnsureInfrastructureDirectories()
    {
        foreach (var path in new[]
                 {
                     Paths.RootDirectory, Paths.DataDirectory, Paths.ToolsDirectory,
                     Paths.AttachmentsDirectory, Paths.BackupsDirectory,
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
