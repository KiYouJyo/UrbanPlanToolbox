namespace UrbanPlanToolbox.Models;

public sealed class BackupManifest
{
    public required int BackupFormatVersion { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public required string ExportedByAppVersion { get; init; }
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
