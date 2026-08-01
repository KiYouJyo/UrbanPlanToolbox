using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Models.Projects;
using UrbanPlanToolbox.Models.Tools;
using UrbanPlanToolbox.Services;
using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class BackupDataServiceTests
{
    [Fact]
    public async Task ExportCreatesManifestHashesAndExpectedPortableStructure()
    {
        using var scope = new BackupScope();
        var project = await scope.CreateProjectAsync();
        var output = scope.Path("export.uptbackup");
        var result = await scope.Backup.ExportAsync(output);
        var inspection = await scope.Backup.InspectAsync(output);
        using var archive = ZipFile.OpenRead(output);

        Assert.True(result.Succeeded);
        Assert.True(inspection.Succeeded);
        Assert.Equal(BackupDataService.BackupFormatVersion, result.Manifest!.BackupFormatVersion);
        Assert.Equal(1, result.Manifest.ProjectCount);
        Assert.Contains(result.Manifest.Files, file => file.RelativePath == $"data/projects/{project.Id:D}/project.json" && file.Sha256.Length == 64);
        Assert.Contains(archive.Entries, entry => entry.FullName == "backup-manifest.json");
        Assert.Contains(archive.Entries, entry => entry.FullName == "settings/settings.json");
    }

    [Fact]
    public async Task ExportExcludesCacheLogsRecoveryFilesAndFolderTokens()
    {
        using var scope = new BackupScope();
        var project = await scope.CreateProjectAsync(withFolder: true);
        Directory.CreateDirectory(scope.Provider.Paths.CacheDirectory); await File.WriteAllTextAsync(System.IO.Path.Combine(scope.Provider.Paths.CacheDirectory, "cache.txt"), "cache");
        Directory.CreateDirectory(scope.Provider.Paths.LogsDirectory); await File.WriteAllTextAsync(System.IO.Path.Combine(scope.Provider.Paths.LogsDirectory, "log.txt"), "log");
        await File.WriteAllTextAsync(scope.Provider.GetProjectBackupFilePath(project.Id), "backup");
        var output = scope.Path("portable.uptbackup"); await scope.Backup.ExportAsync(output);
        using var archive = ZipFile.OpenRead(output);
        var names = archive.Entries.Select(entry => entry.FullName).ToArray();
        Assert.DoesNotContain(names, name => name.Contains("cache", StringComparison.OrdinalIgnoreCase) || name.Contains("logs", StringComparison.OrdinalIgnoreCase) || name.Contains("last-valid", StringComparison.OrdinalIgnoreCase));
        var entry = archive.GetEntry($"data/projects/{project.Id:D}/project.json")!;
        using var reader = new StreamReader(entry.Open()); var json = await reader.ReadToEndAsync();
        Assert.DoesNotContain("local-secret-token", json);
        Assert.Contains("\"requiresReselection\": true", json);
        Assert.Contains("C:\\\\Visible", json);
    }

    [Fact]
    public async Task RoundTripRestoresProjectsTodosSnapshotsArchiveSettingsAndFavorites()
    {
        using var scope = new BackupScope();
        var project = await scope.CreateProjectAsync(withFolder: true);
        await scope.Projects.AddTodoAsync(project.Id, "Review plan");
        await scope.Projects.AddSnapshotAsync(project.Id, new PlanningInput { SiteArea = 3m }, new PlanningResult { FloorAreaRatio = 1m / 3m });
        project = (await scope.Projects.ReadAsync(project.Id)).Value!;
        project.PlanningRequirements = "Preserve the historic street wall.";
        project.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await scope.Projects.SaveAsync(project);
        await scope.Projects.AddMilestoneAsync(project.Id, "Public review", new DateOnly(2026, 9, 1), new TimeOnly(14, 0), "Council hall");
        await scope.Projects.ArchiveAsync(project.Id, true);
        new SettingsService(scope.Provider.Paths.SettingsFilePath).Save(new AppSettings { Theme = "Dark", Language = "ja-JP", FavoriteToolIds = [ToolIds.UnitScaleConverter] });
        var output = scope.Path("roundtrip.uptbackup"); await scope.Backup.ExportAsync(output);

        await scope.Projects.CreateAsync("Changed", ProjectTypeCodes.Personal);
        new SettingsService(scope.Provider.Paths.SettingsFilePath).Save(new AppSettings { Theme = "Light" });
        var imported = await scope.Backup.ImportAsync(output);
        var archived = await scope.Projects.ListAsync(true);
        var settings = new SettingsService(scope.Provider.Paths.SettingsFilePath).Load();

        Assert.True(imported.Succeeded);
        Assert.Single(archived.Projects);
        Assert.Equal(project.Id, archived.Projects[0].Id);
        Assert.Single(archived.Projects[0].Todos);
        Assert.Single(archived.Projects[0].PlanningSnapshots);
        Assert.Equal("Preserve the historic street wall.", archived.Projects[0].PlanningRequirements);
        var milestone = Assert.Single(archived.Projects[0].Milestones);
        Assert.Equal("Public review", milestone.Title);
        Assert.Equal(new DateOnly(2026, 9, 1), milestone.Date);
        Assert.Equal(new TimeOnly(14, 0), milestone.Time);
        var folder = Assert.IsType<ProjectFolderReference>(archived.Projects[0].WorkFolder);
        Assert.Null(folder.AccessToken);
        Assert.True(folder.RequiresReselection);
        Assert.Equal("Dark", settings.Theme);
        Assert.Equal("ja-JP", settings.Language);
        Assert.Equal([ToolIds.UnitScaleConverter], settings.FavoriteToolIds);
    }

    [Fact]
    public async Task CorruptZipAndChecksumMismatchAreRejectedWithoutChangingData()
    {
        using var scope = new BackupScope();
        var original = await scope.CreateProjectAsync();
        var corrupt = scope.Path("corrupt.uptbackup"); await File.WriteAllTextAsync(corrupt, "not a zip");
        Assert.Equal(BackupOperationStatus.InvalidPackage, (await scope.Backup.ImportAsync(corrupt)).Status);

        var valid = scope.Path("checksum.uptbackup"); await scope.Backup.ExportAsync(valid);
        RewriteEntry(valid, $"data/projects/{original.Id:D}/project.json", Encoding.UTF8.GetBytes("tampered"));
        Assert.Equal("ChecksumMismatch", (await scope.Backup.InspectAsync(valid)).FailureType);
        Assert.Equal(original.Id, (await scope.Projects.ListAsync(false)).Projects.Single().Id);
    }

    [Theory]
    [InlineData("../escape.json")]
    [InlineData("C:/escape.json")]
    [InlineData("/absolute.json")]
    public async Task UnsafeArchivePathsAreRejected(string entryName)
    {
        using var scope = new BackupScope();
        var package = scope.Path("unsafe.uptbackup");
        using (var archive = ZipFile.Open(package, ZipArchiveMode.Create)) { using var writer = new StreamWriter(archive.CreateEntry(entryName).Open()); writer.Write("bad"); }
        Assert.Equal(BackupOperationStatus.InvalidPackage, (await scope.Backup.InspectAsync(package)).Status);
    }

    [Fact]
    public async Task DuplicateAndUnlistedEntriesAreRejected()
    {
        using var scope = new BackupScope();
        var duplicate = scope.Path("duplicate.uptbackup");
        using (var archive = ZipFile.Open(duplicate, ZipArchiveMode.Create)) { archive.CreateEntry("backup-manifest.json"); archive.CreateEntry("backup-manifest.json"); }
        Assert.Equal("DuplicateEntry", (await scope.Backup.InspectAsync(duplicate)).FailureType);

        await scope.CreateProjectAsync(); var valid = scope.Path("unlisted.uptbackup"); await scope.Backup.ExportAsync(valid);
        using (var archive = ZipFile.Open(valid, ZipArchiveMode.Update)) { using var writer = new StreamWriter(archive.CreateEntry("attachments/projects/extra.txt").Open()); writer.Write("extra"); }
        Assert.Equal("UnlistedFile", (await scope.Backup.InspectAsync(valid)).FailureType);
    }

    [Fact]
    public async Task FutureBackupAndProjectFormatsAreRejected()
    {
        using var scope = new BackupScope();
        var project = await scope.CreateProjectAsync(); var package = scope.Path("future.uptbackup"); await scope.Backup.ExportAsync(package);
        MutateExtractedPackage(package, root =>
        {
            var manifestPath = System.IO.Path.Combine(root, "backup-manifest.json");
            var manifest = JsonSerializer.Deserialize<BackupManifest>(File.ReadAllText(manifestPath), DataStorageJson.Options)!;
            File.WriteAllText(manifestPath, JsonSerializer.Serialize(new BackupManifest { BackupFormatVersion = 2, CreatedAtUtc = manifest.CreatedAtUtc, ExportedByAppVersion = manifest.ExportedByAppVersion, ProjectCount = manifest.ProjectCount, ActiveProjectCount = manifest.ActiveProjectCount, ArchivedProjectCount = manifest.ArchivedProjectCount, Files = manifest.Files }, DataStorageJson.Options));
        });
        Assert.Equal(BackupOperationStatus.UnsupportedFutureVersion, (await scope.Backup.InspectAsync(package)).Status);

        await scope.Backup.ExportAsync(package);
        MutateExtractedPackage(package, root =>
        {
            var relative = $"data/projects/{project.Id:D}/project.json"; var path = System.IO.Path.Combine(root, relative.Replace('/', System.IO.Path.DirectorySeparatorChar));
            var json = File.ReadAllText(path).Replace("\"schemaVersion\": 2", "\"schemaVersion\": 3"); File.WriteAllText(path, json);
            RefreshManifestFile(root, relative);
        });
        Assert.Equal("FutureProjectFormat", (await scope.Backup.InspectAsync(package)).FailureType);
    }

    [Fact]
    public async Task SchemaOneProjectInBackupIsAcceptedAndMigratedOnImport()
    {
        using var scope = new BackupScope();
        var project = await scope.CreateProjectAsync();
        await scope.Projects.AddTodoAsync(project.Id, "Legacy backup todo");
        var package = scope.Path("schema-one.uptbackup");
        await scope.Backup.ExportAsync(package);
        MutateExtractedPackage(package, root =>
        {
            var relative = $"data/projects/{project.Id:D}/project.json";
            var path = System.IO.Path.Combine(root, relative.Replace('/', System.IO.Path.DirectorySeparatorChar));
            File.WriteAllText(path, File.ReadAllText(path).Replace("\"schemaVersion\": 2", "\"schemaVersion\": 1"));
            RefreshManifestFile(root, relative);
        });

        Assert.True((await scope.Backup.InspectAsync(package)).Succeeded);
        Assert.True((await scope.Backup.ImportAsync(package)).Succeeded);
        var read = await scope.Projects.ReadAsync(project.Id);
        Assert.Equal(2, read.SchemaVersion);
        Assert.Equal("Legacy backup todo", Assert.Single(read.Value!.Todos).Title);
        Assert.Empty(read.Value.Milestones);
    }

    [Fact]
    public async Task PreImportBackupFailureLeavesCurrentDataUntouched()
    {
        using var scope = new BackupScope(); var original = await scope.CreateProjectAsync(); var package = scope.Path("safe.uptbackup"); await scope.Backup.ExportAsync(package);
        await scope.Projects.CreateAsync("Current", ProjectTypeCodes.Personal);
        var failing = new BackupDataService(scope.Provider, "0.3.9", operation => operation == "PreImportBackup");
        var result = await failing.ImportAsync(package);
        Assert.Equal(BackupOperationStatus.PreImportBackupFailed, result.Status);
        Assert.Equal(2, (await scope.Projects.ListAsync(false)).Projects.Count);
        Assert.Contains((await scope.Projects.ListAsync(false)).Projects, item => item.Id == original.Id);
    }

    [Fact]
    public async Task ReplacementFailureRollsBackCurrentData()
    {
        using var scope = new BackupScope(); await scope.CreateProjectAsync(); var package = scope.Path("rollback.uptbackup"); await scope.Backup.ExportAsync(package);
        var current = (await scope.Projects.CreateAsync("Current", ProjectTypeCodes.Personal)).Project!;
        var failing = new BackupDataService(scope.Provider, "0.3.9", operation => operation == "Replace");
        var result = await failing.ImportAsync(package);
        Assert.Equal(BackupOperationStatus.ReplacementFailed, result.Status);
        Assert.True(result.RollbackSucceeded);
        Assert.Contains((await scope.Projects.ListAsync(false)).Projects, project => project.Id == current.Id);
    }

    [Fact]
    public async Task ExportFailureDoesNotLeaveAValidLookingFile()
    {
        using var scope = new BackupScope(); await scope.CreateProjectAsync(); var directory = scope.Path("destination"); Directory.CreateDirectory(directory);
        var result = await scope.Backup.ExportAsync(directory);
        Assert.False(result.Succeeded);
        Assert.True(Directory.Exists(directory));
        Assert.Empty(Directory.GetFiles(directory));
    }

    private static void RewriteEntry(string package, string name, byte[] content)
    {
        using var archive = ZipFile.Open(package, ZipArchiveMode.Update); archive.GetEntry(name)!.Delete(); using var stream = archive.CreateEntry(name).Open(); stream.Write(content);
    }

    private static void MutateExtractedPackage(string package, Action<string> mutation)
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"upt-test-{Guid.NewGuid():N}"); Directory.CreateDirectory(root);
        try { ZipFile.ExtractToDirectory(package, root); mutation(root); File.Delete(package); ZipFile.CreateFromDirectory(root, package); }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private static void RefreshManifestFile(string root, string relative)
    {
        var manifestPath = System.IO.Path.Combine(root, "backup-manifest.json"); var manifest = JsonSerializer.Deserialize<BackupManifest>(File.ReadAllText(manifestPath), DataStorageJson.Options)!;
        var path = System.IO.Path.Combine(root, relative.Replace('/', System.IO.Path.DirectorySeparatorChar)); var info = new FileInfo(path); var item = manifest.Files.Single(file => file.RelativePath == relative);
        var replacement = new BackupManifestFile { RelativePath = relative, Size = info.Length, Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant() };
        manifest.Files[manifest.Files.IndexOf(item)] = replacement; File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, DataStorageJson.Options));
    }

    private sealed class BackupScope : IDisposable
    {
        public BackupScope()
        {
            Root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"UrbanPlanToolbox-backup-{Guid.NewGuid():N}"); Provider = new AppDataPathProvider(System.IO.Path.Combine(Root, "app"), [ToolIds.PlanningIndicatorCalculator, ToolIds.UnitScaleConverter]); Provider.EnsureInfrastructureDirectories(); Projects = new ProjectStorageService(Provider); Backup = new BackupDataService(Provider, "0.3.9");
        }
        public string Root { get; } public AppDataPathProvider Provider { get; } public ProjectStorageService Projects { get; } public BackupDataService Backup { get; }
        public string Path(string name) => System.IO.Path.Combine(Root, name);
        public async Task<ProjectRecord> CreateProjectAsync(bool withFolder = false)
        {
            var project = (await Projects.CreateAsync("Backup project", ProjectTypeCodes.Research, administrativeArea: "Hangzhou", latitude: 30m, longitude: 120m)).Project!;
            if (withFolder) { project.WorkFolder = new() { AccessToken = "local-secret-token", DisplayName = "Visible", DisplayPath = "C:\\Visible" }; project.UpdatedAtUtc = DateTimeOffset.UtcNow; await Projects.SaveAsync(project); }
            return project;
        }
        public void Dispose() { if (Directory.Exists(Root)) Directory.Delete(Root, true); }
    }
}
