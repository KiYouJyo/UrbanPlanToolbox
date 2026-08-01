using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Models.Projects;

namespace UrbanPlanToolbox.Services;

public sealed class BackupDataService
{
    public const int BackupFormatVersion = 1;
    public const int MaximumFileCount = 10_000;
    public const long MaximumSingleFileBytes = 256L * 1024 * 1024;
    public const long MaximumPackageBytes = 2L * 1024 * 1024 * 1024;
    private static readonly HashSet<string> BlockedExtensions = new(StringComparer.OrdinalIgnoreCase)
    { ".exe", ".dll", ".com", ".bat", ".cmd", ".ps1", ".msi", ".msix", ".appx", ".pfx", ".cer" };

    private readonly IAppDataPathProvider _paths;
    private readonly string _appVersion;
    private readonly Func<string, bool>? _failureInjector;

    public BackupDataService(IAppDataPathProvider paths, string appVersion, Func<string, bool>? failureInjector = null)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _appVersion = appVersion ?? throw new ArgumentNullException(nameof(appVersion));
        _failureInjector = failureInjector;
    }

    public async Task<BackupOperationResult> ExportAsync(string outputPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        var temporaryRoot = Path.Combine(Path.GetTempPath(), $"UrbanPlanToolbox-export-{Guid.NewGuid():N}");
        var packagePath = Path.Combine(Path.GetTempPath(), $"UrbanPlanToolbox-{Guid.NewGuid():N}.uptbackup.tmp");
        try
        {
            Directory.CreateDirectory(temporaryRoot);
            var projects = await ReadAllProjectsAsync(cancellationToken).ConfigureAwait(false);
            if (projects.Issues.Count > 0) return new(BackupOperationStatus.InvalidPackage, FailureType: "ProjectReadFailed");

            var portableSettingsPath = Path.Combine(temporaryRoot, "settings", "settings.json");
            if (File.Exists(_paths.Paths.SettingsFilePath)) CopyFile(_paths.Paths.SettingsFilePath, portableSettingsPath);
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(portableSettingsPath)!);
                await File.WriteAllTextAsync(portableSettingsPath, JsonSerializer.Serialize(new AppSettings()), new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
            }
            await WritePortableProjectsAsync(temporaryRoot, projects.Projects, cancellationToken).ConfigureAwait(false);
            CopyDirectory(_paths.Paths.ProjectAttachmentsDirectory, Path.Combine(temporaryRoot, "attachments", "projects"), excludeInternalFiles: false);

            var files = Directory.GetFiles(temporaryRoot, "*", SearchOption.AllDirectories)
                .Select(path => CreateManifestFile(temporaryRoot, path)).OrderBy(file => file.RelativePath, StringComparer.Ordinal).ToList();
            ValidateLimits(files.Count, files.Sum(file => file.Size), files.Select(file => file.Size));
            var manifest = new BackupManifest
            {
                BackupFormatVersion = BackupFormatVersion, CreatedAtUtc = DateTimeOffset.UtcNow,
                ExportedByAppVersion = _appVersion, ProjectCount = projects.Projects.Count,
                ActiveProjectCount = projects.Projects.Count(project => !project.IsArchived),
                ArchivedProjectCount = projects.Projects.Count(project => project.IsArchived), Files = files
            };
            await File.WriteAllTextAsync(Path.Combine(temporaryRoot, "backup-manifest.json"), JsonSerializer.Serialize(manifest, DataStorageJson.Options), new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
            ZipFile.CreateFromDirectory(temporaryRoot, packagePath, CompressionLevel.Optimal, includeBaseDirectory: false);
            var inspection = await InspectAsync(packagePath, cancellationToken).ConfigureAwait(false);
            if (!inspection.Succeeded) throw new InvalidDataException("ExportedPackageVerificationFailed");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
            File.Move(packagePath, outputPath, overwrite: true);
            return new(BackupOperationStatus.Success, manifest, new FileInfo(outputPath).Length);
        }
        catch (BackupLimitException exception) { return new(BackupOperationStatus.LimitExceeded, FailureType: exception.Message); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
        { return new(BackupOperationStatus.IoFailure, FailureType: exception.GetType().Name); }
        finally { TryDeleteDirectory(temporaryRoot); TryDeleteFile(packagePath); }
    }

    public async Task<BackupInspection> InspectAsync(string packagePath, CancellationToken cancellationToken = default)
    {
        try
        {
            var package = new FileInfo(packagePath);
            if (!package.Exists) return new(BackupOperationStatus.InvalidPackage, FailureType: "PackageNotFound");
            if (package.Length > MaximumPackageBytes) return new(BackupOperationStatus.LimitExceeded, FailureType: "PackageTooLarge");
            using var archive = ZipFile.OpenRead(packagePath);
            if (archive.Entries.Count > MaximumFileCount + 1) return new(BackupOperationStatus.LimitExceeded, FailureType: "TooManyFiles");
            var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in archive.Entries)
            {
                var path = NormalizeEntryPath(entry.FullName);
                if (!entries.TryAdd(path, entry)) return new(BackupOperationStatus.InvalidPackage, FailureType: "DuplicateEntry");
                if (entry.Length > MaximumSingleFileBytes) return new(BackupOperationStatus.LimitExceeded, FailureType: "FileTooLarge");
                if (!IsAllowedPath(path)) return new(BackupOperationStatus.InvalidPackage, FailureType: "UnexpectedFile");
            }
            ValidateLimits(entries.Count - 1, entries.Values.Where(entry => !entry.FullName.EndsWith('/')).Sum(entry => entry.Length), entries.Values.Select(entry => entry.Length));
            if (!entries.TryGetValue("backup-manifest.json", out var manifestEntry)) return new(BackupOperationStatus.InvalidPackage, FailureType: "ManifestMissing");
            BackupManifest? manifest;
            await using (var stream = manifestEntry.Open()) manifest = await JsonSerializer.DeserializeAsync<BackupManifest>(stream, DataStorageJson.Options, cancellationToken).ConfigureAwait(false);
            if (manifest is null) return new(BackupOperationStatus.InvalidPackage, FailureType: "ManifestInvalid");
            if (manifest.BackupFormatVersion > BackupFormatVersion) return new(BackupOperationStatus.UnsupportedFutureVersion, manifest, "FutureBackupFormat");
            if (manifest.BackupFormatVersion != BackupFormatVersion || manifest.CreatedAtUtc.Offset != TimeSpan.Zero) return new(BackupOperationStatus.InvalidPackage, manifest, "ManifestInvalid");
            var listed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in manifest.Files)
            {
                var path = NormalizeEntryPath(file.RelativePath);
                if (!listed.Add(path) || path == "backup-manifest.json" || !entries.TryGetValue(path, out var entry)) return new(BackupOperationStatus.InvalidPackage, manifest, "ManifestFileMismatch");
                if (entry.Length != file.Size || !string.Equals(await HashEntryAsync(entry, cancellationToken).ConfigureAwait(false), file.Sha256, StringComparison.OrdinalIgnoreCase)) return new(BackupOperationStatus.InvalidPackage, manifest, "ChecksumMismatch");
            }
            if (entries.Keys.Where(path => path != "backup-manifest.json" && !path.EndsWith('/')).Any(path => !listed.Contains(path))) return new(BackupOperationStatus.InvalidPackage, manifest, "UnlistedFile");
            if (entries.TryGetValue("settings/settings.json", out var settingsEntry))
            {
                await using var settingsStream = settingsEntry.Open();
                if (await JsonSerializer.DeserializeAsync<AppSettings>(settingsStream, cancellationToken: cancellationToken).ConfigureAwait(false) is null)
                    return new(BackupOperationStatus.InvalidPackage, manifest, "SettingsInvalid");
            }
            var projectValidation = await ValidateProjectsAsync(entries, cancellationToken).ConfigureAwait(false);
            return projectValidation is null ? new(BackupOperationStatus.Success, manifest) : projectValidation;
        }
        catch (BackupLimitException exception) { return new(BackupOperationStatus.LimitExceeded, FailureType: exception.Message); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or JsonException or NotSupportedException)
        { return new(BackupOperationStatus.InvalidPackage, FailureType: exception.GetType().Name); }
    }

    public async Task<BackupOperationResult> ImportAsync(string packagePath, CancellationToken cancellationToken = default)
    {
        var inspection = await InspectAsync(packagePath, cancellationToken).ConfigureAwait(false);
        if (!inspection.Succeeded) return new(inspection.Status, inspection.Manifest, FailureType: inspection.FailureType);
        var staging = Path.Combine(_paths.Paths.RootDirectory, $".import-{Guid.NewGuid():N}");
        string? safetyBackup = null;
        try
        {
            if (_failureInjector?.Invoke("PreImportBackup") == true) throw new IOException("InjectedPreImportBackupFailure");
            safetyBackup = CreatePreImportBackup();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        { return new(BackupOperationStatus.PreImportBackupFailed, inspection.Manifest, FailureType: exception.GetType().Name); }

        try
        {
            Directory.CreateDirectory(staging);
            ExtractValidatedPackage(packagePath, staging);
            MarkFolderReferencesForReselection(staging);
            ReplaceOfficialData(staging);
            return new(BackupOperationStatus.Success, inspection.Manifest);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
        {
            var restored = safetyBackup is not null && RestorePreImportBackup(safetyBackup);
            return new(BackupOperationStatus.ReplacementFailed, inspection.Manifest, FailureType: exception.GetType().Name, RollbackSucceeded: restored);
        }
        finally { TryDeleteDirectory(staging); }
    }

    private async Task<ProjectListResult> ReadAllProjectsAsync(CancellationToken cancellationToken)
    {
        var service = new ProjectStorageService(_paths);
        var active = await service.ListAsync(false, cancellationToken).ConfigureAwait(false);
        var archived = await service.ListAsync(true, cancellationToken).ConfigureAwait(false);
        return new(active.Projects.Concat(archived.Projects).ToArray(), active.Issues.Concat(archived.Issues).ToArray());
    }

    private async Task WritePortableProjectsAsync(string root, IReadOnlyList<ProjectRecord> projects, CancellationToken cancellationToken)
    {
        var index = new ProjectIndex { Projects = projects.Select(project => new ProjectIndexEntry { Id = project.Id, Kind = project.Kind, Name = project.Name, Type = project.Type, IsArchived = project.IsArchived, UpdatedAtUtc = project.UpdatedAtUtc, ArchivedAtUtc = project.ArchivedAtUtc }).ToList() };
        await WriteEnvelopeAsync(Path.Combine(root, "data", "projects", "index.json"), index, cancellationToken).ConfigureAwait(false);
        foreach (var source in projects)
        {
            var project = JsonSerializer.Deserialize<ProjectRecord>(JsonSerializer.Serialize(source, DataStorageJson.Options), DataStorageJson.Options)!;
            if (project.WorkFolder is not null) { project.WorkFolder.AccessToken = null; project.WorkFolder.RequiresReselection = true; }
            await WriteEnvelopeAsync(Path.Combine(root, "data", "projects", project.Id.ToString("D"), "project.json"), project, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task WriteEnvelopeAsync<T>(string path, T payload, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var envelope = new DataEnvelope<T> { SchemaVersion = ProjectStorageService.ProjectSchemaVersion, SavedAtUtc = DateTimeOffset.UtcNow, Payload = payload };
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(envelope, DataStorageJson.Options), new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
    }

    private static BackupManifestFile CreateManifestFile(string root, string path)
    {
        var info = new FileInfo(path);
        return new() { RelativePath = Path.GetRelativePath(root, path).Replace('\\', '/'), Size = info.Length, Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant() };
    }

    private static async Task<string> HashEntryAsync(ZipArchiveEntry entry, CancellationToken cancellationToken)
    {
        await using var stream = entry.Open();
        using var sha = SHA256.Create();
        return Convert.ToHexString(await sha.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false)).ToLowerInvariant();
    }

    private static string NormalizeEntryPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Contains(':')) throw new InvalidDataException("UnsafePath");
        var normalized = path.Replace('\\', '/');
        if (normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(segment => segment is "." or "..")) throw new InvalidDataException("UnsafePath");
        return normalized;
    }

    private static bool IsAllowedPath(string path)
    {
        if (path.EndsWith('/')) return true;
        if (BlockedExtensions.Contains(Path.GetExtension(path))) return false;
        return path == "backup-manifest.json" || path == "settings/settings.json" ||
               path == "data/projects/index.json" ||
               path.StartsWith("data/projects/", StringComparison.Ordinal) && path.EndsWith("/project.json", StringComparison.Ordinal) ||
               path.StartsWith("attachments/projects/", StringComparison.Ordinal);
    }

    private static async Task<BackupInspection?> ValidateProjectsAsync(IReadOnlyDictionary<string, ZipArchiveEntry> entries, CancellationToken cancellationToken)
    {
        foreach (var (path, entry) in entries.Where(pair => pair.Key.StartsWith("data/projects/", StringComparison.Ordinal) && pair.Key.EndsWith(".json", StringComparison.Ordinal)))
        {
            await using var stream = entry.Open();
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (!document.RootElement.TryGetProperty("schemaVersion", out var version) || version.GetInt32() > ProjectStorageService.ProjectSchemaVersion) return new(BackupOperationStatus.UnsupportedFutureVersion, FailureType: "FutureProjectFormat");
            if (version.GetInt32() < 1 || !document.RootElement.TryGetProperty("payload", out _)) return new(BackupOperationStatus.InvalidPackage, FailureType: "ProjectEnvelopeInvalid");
            if (path != "data/projects/index.json")
            {
                var idSegment = path.Split('/')[2];
                if (!Guid.TryParse(idSegment, out var id) || !document.RootElement.GetProperty("payload").TryGetProperty("id", out var payloadId) || payloadId.GetGuid() != id) return new(BackupOperationStatus.InvalidPackage, FailureType: "ProjectIdMismatch");
            }
        }
        return null;
    }

    private void ExtractValidatedPackage(string packagePath, string staging)
    {
        using var archive = ZipFile.OpenRead(packagePath);
        foreach (var entry in archive.Entries)
        {
            var path = NormalizeEntryPath(entry.FullName);
            if (path == "backup-manifest.json" || path.EndsWith('/')) continue;
            var destination = Path.GetFullPath(Path.Combine(staging, path.Replace('/', Path.DirectorySeparatorChar)));
            var root = Path.GetFullPath(staging) + Path.DirectorySeparatorChar;
            if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("UnsafePath");
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!); entry.ExtractToFile(destination, overwrite: false);
        }
    }

    private static void MarkFolderReferencesForReselection(string staging)
    {
        var projectRoot = Path.Combine(staging, "data", "projects");
        if (!Directory.Exists(projectRoot)) return;
        foreach (var path in Directory.GetFiles(projectRoot, "project.json", SearchOption.AllDirectories))
        {
            var document = JsonNode.Parse(File.ReadAllText(path))?.AsObject() ?? throw new InvalidDataException("ProjectEnvelopeInvalid");
            if (document["payload"] is JsonObject payload && payload["workFolder"] is JsonObject folder)
            {
                folder["accessToken"] = null;
                folder["requiresReselection"] = true;
            }
            File.WriteAllText(path, document.ToJsonString(DataStorageJson.Options), new UTF8Encoding(false));
        }
    }

    private string CreatePreImportBackup()
    {
        var parent = _paths.GetPreImportBackupDirectory();
        foreach (var old in Directory.GetDirectories(parent)) TryDeleteDirectory(old);
        var destination = Path.Combine(parent, DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff"));
        Directory.CreateDirectory(destination);
        if (File.Exists(_paths.Paths.SettingsFilePath)) CopyFile(_paths.Paths.SettingsFilePath, Path.Combine(destination, "settings.json"));
        CopyDirectory(_paths.Paths.ProjectsDirectory, Path.Combine(destination, "projects"), excludeInternalFiles: true);
        CopyDirectory(_paths.Paths.ProjectAttachmentsDirectory, Path.Combine(destination, "attachments-projects"), excludeInternalFiles: false);
        return destination;
    }

    private void ReplaceOfficialData(string staging)
    {
        ReplaceFile(Path.Combine(staging, "settings", "settings.json"), _paths.Paths.SettingsFilePath);
        if (_failureInjector?.Invoke("Replace") == true) throw new IOException("InjectedReplacementFailure");
        ReplaceDirectory(Path.Combine(staging, "data", "projects"), _paths.Paths.ProjectsDirectory);
        ReplaceDirectory(Path.Combine(staging, "attachments", "projects"), _paths.Paths.ProjectAttachmentsDirectory);
    }

    private bool RestorePreImportBackup(string backup)
    {
        try
        {
            ReplaceFile(Path.Combine(backup, "settings.json"), _paths.Paths.SettingsFilePath);
            ReplaceDirectory(Path.Combine(backup, "projects"), _paths.Paths.ProjectsDirectory);
            ReplaceDirectory(Path.Combine(backup, "attachments-projects"), _paths.Paths.ProjectAttachmentsDirectory);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return false; }
    }

    private static void ReplaceFile(string source, string destination) { if (File.Exists(destination)) File.Delete(destination); if (File.Exists(source)) { Directory.CreateDirectory(Path.GetDirectoryName(destination)!); File.Copy(source, destination); } }
    private static void ReplaceDirectory(string source, string destination) { if (Directory.Exists(destination)) Directory.Delete(destination, true); if (Directory.Exists(source)) CopyDirectory(source, destination, false); else Directory.CreateDirectory(destination); }
    private static void CopyFile(string source, string destination) { Directory.CreateDirectory(Path.GetDirectoryName(destination)!); File.Copy(source, destination, overwrite: true); }
    private static void CopyDirectory(string source, string destination, bool excludeInternalFiles)
    {
        if (!Directory.Exists(source)) return;
        foreach (var path in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            if (excludeInternalFiles && (path.EndsWith(".last-valid.bak", StringComparison.OrdinalIgnoreCase) || Path.GetFileName(path).Contains(".corrupt-", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))) continue;
            CopyFile(path, Path.Combine(destination, Path.GetRelativePath(source, path)));
        }
    }
    private static void ValidateLimits(int count, long total, IEnumerable<long> sizes) { if (count > MaximumFileCount) throw new BackupLimitException("TooManyFiles"); if (total > MaximumPackageBytes) throw new BackupLimitException("PackageTooLarge"); if (sizes.Any(size => size > MaximumSingleFileBytes)) throw new BackupLimitException("FileTooLarge"); }
    private static void TryDeleteDirectory(string path) { try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    private static void TryDeleteFile(string path) { try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    private sealed class BackupLimitException(string message) : Exception(message);
}
