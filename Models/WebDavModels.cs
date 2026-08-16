using System.Globalization;

namespace UrbanPlanToolbox.Models;

public sealed record WebDavProfile
{
    public string ServerUrl { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string RemotePath { get; init; } = "/UrbanPlanToolbox/Backups";
    public DateTimeOffset? LastBackupAtUtc { get; init; }
}

public enum WebDavStatus
{
    Success,
    InvalidConfiguration,
    AuthenticationFailed,
    Forbidden,
    NotFound,
    Conflict,
    Timeout,
    TransportFailure,
    ServerFailure,
    ProtocolFailure,
    IoFailure
}

public sealed record WebDavResult(WebDavStatus Status, string? ErrorCode = null)
{
    public bool Succeeded => Status == WebDavStatus.Success;
}

public sealed record WebDavListResult(
    WebDavStatus Status,
    IReadOnlyList<CloudBackupItem> Items,
    string? ErrorCode = null)
{
    public bool Succeeded => Status == WebDavStatus.Success;
}

public sealed record CloudBackupItem(
    string FileName,
    long Size,
    DateTimeOffset? CreatedAtUtc,
    DateTimeOffset? LastModifiedAtUtc,
    string? AppVersion)
{
    public DateTimeOffset SortTimeUtc => CreatedAtUtc ?? LastModifiedAtUtc ?? DateTimeOffset.MinValue;

    public static bool TryParseFileName(string fileName, out DateTimeOffset createdAtUtc, out string version)
    {
        const string prefix = "UrbanPlanToolbox-";
        const string suffix = ".uptbackup";
        createdAtUtc = default;
        version = string.Empty;
        if (!fileName.StartsWith(prefix, StringComparison.Ordinal) || !fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) return false;
        var core = fileName[prefix.Length..^suffix.Length];
        var marker = core.LastIndexOf("-v", StringComparison.Ordinal);
        if (marker <= 0 || marker >= core.Length - 2) return false;
        var timestamp = core[..marker];
        version = core[(marker + 2)..];
        return DateTimeOffset.TryParseExact(
            timestamp,
            "yyyyMMdd'T'HHmmss'Z'",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out createdAtUtc);
    }
}

public enum CloudBackupStatus
{
    Success,
    NotConfigured,
    InvalidConfiguration,
    CredentialUnavailable,
    AuthenticationFailed,
    Forbidden,
    NotFound,
    Conflict,
    Timeout,
    TransportFailure,
    ServerFailure,
    ProtocolFailure,
    BackupExportFailed,
    BackupValidationFailed,
    BackupImportFailed,
    IoFailure
}

public sealed record CloudBackupResult(
    CloudBackupStatus Status,
    string? ErrorCode = null,
    BackupManifest? Manifest = null,
    long? FileSize = null)
{
    public bool Succeeded => Status == CloudBackupStatus.Success;
}

public sealed record CloudBackupListResult(
    CloudBackupStatus Status,
    IReadOnlyList<CloudBackupItem> Items,
    string? ErrorCode = null)
{
    public bool Succeeded => Status == CloudBackupStatus.Success;
}
