using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.Services;

public sealed class CloudBackupService
{
    private readonly IAppDataPathProvider _paths;
    private readonly WebDavProfileService _profileService;
    private readonly IWebDavCredentialStore _credentialStore;
    private readonly IWebDavClient _client;
    private readonly string _appVersion;

    public static CloudBackupService Default { get; } = new(
        AppDataPathProvider.Default,
        WebDavProfileService.Default,
        WebDavCredentialStore.Default,
        new WebDavClient(),
        AppVersionProvider.Version);

    public CloudBackupService(
        IAppDataPathProvider paths,
        WebDavProfileService profileService,
        IWebDavCredentialStore credentialStore,
        IWebDavClient client,
        string appVersion)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _profileService = profileService ?? throw new ArgumentNullException(nameof(profileService));
        _credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _appVersion = string.IsNullOrWhiteSpace(appVersion) ? throw new ArgumentException("App version is required.", nameof(appVersion)) : appVersion.TrimStart('v');
    }

    public Task<WebDavProfile?> GetProfileAsync(CancellationToken cancellationToken = default) => _profileService.LoadAsync(cancellationToken);

    public bool HasCredential(WebDavProfile profile) => _credentialStore.HasCredential(profile.Username);

    public async Task<CloudBackupResult> TestAndSaveAsync(WebDavProfile requestedProfile, string? requestedPassword, CancellationToken cancellationToken = default)
    {
        if (!WebDavProfileService.TryNormalize(requestedProfile, out var normalized, out var errorCode)) return new(CloudBackupStatus.InvalidConfiguration, errorCode);
        var existing = await _profileService.LoadAsync(cancellationToken).ConfigureAwait(false);
        var password = requestedPassword;
        if (string.IsNullOrWhiteSpace(password) && existing is not null && string.Equals(existing.Username, normalized.Username, StringComparison.Ordinal))
            password = _credentialStore.GetPassword(existing.Username);
        if (string.IsNullOrWhiteSpace(password)) return new(CloudBackupStatus.CredentialUnavailable, "PasswordRequired");

        var test = await _client.TestConnectionAsync(normalized, password, cancellationToken).ConfigureAwait(false);
        if (!test.Succeeded) return FromWebDav(test);
        normalized = normalized with { LastBackupAtUtc = existing?.LastBackupAtUtc };
        if (!await _profileService.SaveAsync(normalized, cancellationToken).ConfigureAwait(false)) return new(CloudBackupStatus.IoFailure, "ProfileSaveFailed");
        if (existing is not null && !string.Equals(existing.Username, normalized.Username, StringComparison.Ordinal)) _credentialStore.Delete(existing.Username);
        _credentialStore.Save(normalized.Username, password);
        return new(CloudBackupStatus.Success);
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        var profile = await _profileService.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (profile is not null) _credentialStore.Delete(profile.Username);
        await _profileService.DeleteAsync().ConfigureAwait(false);
    }

    public async Task<CloudBackupResult> CreateAsync(CancellationToken cancellationToken = default)
    {
        var connection = await ResolveConnectionAsync(cancellationToken).ConfigureAwait(false);
        if (connection.Result is not null) return connection.Result;
        var profile = connection.Profile!;
        var password = connection.Password!;
        var createdAtUtc = DateTimeOffset.UtcNow;
        var fileName = $"UrbanPlanToolbox-{createdAtUtc:yyyyMMdd'T'HHmmss'Z'}-v{_appVersion}.uptbackup";
        var tempPath = Path.Combine(Path.GetTempPath(), $"UrbanPlanToolbox-cloud-{Guid.NewGuid():N}.uptbackup");
        try
        {
            var backup = await new BackupDataService(_paths, _appVersion).ExportAsync(tempPath, cancellationToken).ConfigureAwait(false);
            if (!backup.Succeeded) return new(CloudBackupStatus.BackupExportFailed, backup.FailureType, backup.Manifest);
            var upload = await _client.UploadAsync(profile, password, tempPath, fileName, cancellationToken).ConfigureAwait(false);
            if (!upload.Succeeded) return FromWebDav(upload, backup.Manifest, backup.FileSize);
            _ = await _profileService.UpdateLastBackupAsync(createdAtUtc, cancellationToken).ConfigureAwait(false);
            return new(CloudBackupStatus.Success, Manifest: backup.Manifest, FileSize: backup.FileSize);
        }
        finally { TryDelete(tempPath); }
    }

    public async Task<CloudBackupListResult> ListAsync(CancellationToken cancellationToken = default)
    {
        var connection = await ResolveConnectionAsync(cancellationToken).ConfigureAwait(false);
        if (connection.Result is not null) return new(connection.Result.Status, [], connection.Result.ErrorCode);
        var result = await _client.ListAsync(connection.Profile!, connection.Password!, cancellationToken).ConfigureAwait(false);
        return result.Succeeded
            ? new(CloudBackupStatus.Success, result.Items)
            : new(MapStatus(result.Status), [], result.ErrorCode);
    }

    public async Task<CloudBackupResult> RestoreAsync(CloudBackupItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        var connection = await ResolveConnectionAsync(cancellationToken).ConfigureAwait(false);
        if (connection.Result is not null) return connection.Result;
        var tempPath = Path.Combine(Path.GetTempPath(), $"UrbanPlanToolbox-cloud-restore-{Guid.NewGuid():N}.uptbackup");
        try
        {
            var download = await _client.DownloadAsync(connection.Profile!, connection.Password!, item.FileName, tempPath, cancellationToken).ConfigureAwait(false);
            if (!download.Succeeded) return FromWebDav(download);
            var backupService = new BackupDataService(_paths, _appVersion);
            var inspection = await backupService.InspectAsync(tempPath, cancellationToken).ConfigureAwait(false);
            if (!inspection.Succeeded) return new(CloudBackupStatus.BackupValidationFailed, inspection.FailureType, inspection.Manifest);
            var import = await backupService.ImportAsync(tempPath, cancellationToken).ConfigureAwait(false);
            return import.Succeeded
                ? new(CloudBackupStatus.Success, Manifest: import.Manifest)
                : new(CloudBackupStatus.BackupImportFailed, import.FailureType, import.Manifest);
        }
        finally { TryDelete(tempPath); }
    }

    public async Task<CloudBackupResult> DeleteAsync(CloudBackupItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        var connection = await ResolveConnectionAsync(cancellationToken).ConfigureAwait(false);
        if (connection.Result is not null) return connection.Result;
        var result = await _client.DeleteAsync(connection.Profile!, connection.Password!, item.FileName, cancellationToken).ConfigureAwait(false);
        return result.Succeeded ? new(CloudBackupStatus.Success) : FromWebDav(result);
    }

    private async Task<(WebDavProfile? Profile, string? Password, CloudBackupResult? Result)> ResolveConnectionAsync(CancellationToken cancellationToken)
    {
        var profile = await _profileService.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (profile is null) return (null, null, new(CloudBackupStatus.NotConfigured, "ProfileMissing"));
        var password = _credentialStore.GetPassword(profile.Username);
        return string.IsNullOrWhiteSpace(password)
            ? (profile, null, new(CloudBackupStatus.CredentialUnavailable, "CredentialMissing"))
            : (profile, password, null);
    }

    private static CloudBackupResult FromWebDav(WebDavResult result, BackupManifest? manifest = null, long? fileSize = null) =>
        new(MapStatus(result.Status), result.ErrorCode, manifest, fileSize);

    private static CloudBackupStatus MapStatus(WebDavStatus status) => status switch
    {
        WebDavStatus.Success => CloudBackupStatus.Success,
        WebDavStatus.InvalidConfiguration => CloudBackupStatus.InvalidConfiguration,
        WebDavStatus.AuthenticationFailed => CloudBackupStatus.AuthenticationFailed,
        WebDavStatus.Forbidden => CloudBackupStatus.Forbidden,
        WebDavStatus.NotFound => CloudBackupStatus.NotFound,
        WebDavStatus.Conflict => CloudBackupStatus.Conflict,
        WebDavStatus.Timeout => CloudBackupStatus.Timeout,
        WebDavStatus.TransportFailure => CloudBackupStatus.TransportFailure,
        WebDavStatus.ServerFailure => CloudBackupStatus.ServerFailure,
        WebDavStatus.IoFailure => CloudBackupStatus.IoFailure,
        _ => CloudBackupStatus.ProtocolFailure
    };

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
    }
}
