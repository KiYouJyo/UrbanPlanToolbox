using System.Text.Json.Serialization;

namespace UrbanPlanToolbox.Models;

public sealed class BackupManifest
{
    public const string ExpectedFormat = "UrbanPlanToolbox Backup";

    [JsonPropertyName("format")]
    public string? Format { get; init; }

    [JsonPropertyName("formatVersion")]
    public int FormatVersion { get; init; }

    [JsonPropertyName("backupFormatVersion")]
    public int? LegacyBackupFormatVersion { get; init; }

    [JsonIgnore]
    public int BackupFormatVersion
    {
        get => FormatVersion > 0 ? FormatVersion : LegacyBackupFormatVersion ?? 0;
        init => FormatVersion = value;
    }

    [JsonPropertyName("dataSchemaVersion")]
    public int? DataSchemaVersion { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }

    [JsonPropertyName("createdWith")]
    public string? CreatedWith { get; init; }

    [JsonPropertyName("exportedByAppVersion")]
    public string? LegacyExportedByAppVersion { get; init; }

    [JsonIgnore]
    public string ExportedByAppVersion
    {
        get => CreatedWith ?? LegacyExportedByAppVersion ?? string.Empty;
        init => CreatedWith = value;
    }
    public int ProjectCount { get; init; }
    public int ActiveProjectCount { get; init; }
    public int ArchivedProjectCount { get; init; }
    public List<BackupManifestFile> Files { get; init; } = [];
}

public sealed class BackupManifestFile
{
    public required string RelativePath { get; init; }
    public required long Size { get; init; }
    public required string Sha256 { get; init; }
}

public enum BackupOperationStatus
{
    Success,
    InvalidPackage,
    UnsupportedFutureVersion,
    LimitExceeded,
    PreImportBackupFailed,
    ReplacementFailed,
    IoFailure
}

public sealed record BackupInspection(
    BackupOperationStatus Status,
    BackupManifest? Manifest = null,
    string? FailureType = null)
{
    public bool Succeeded => Status == BackupOperationStatus.Success;
}

public sealed record BackupOperationResult(
    BackupOperationStatus Status,
    BackupManifest? Manifest = null,
    long FileSize = 0,
    string? FailureType = null,
    bool RollbackSucceeded = false)
{
    public bool Succeeded => Status == BackupOperationStatus.Success;
}
