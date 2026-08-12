using System.Runtime.InteropServices;
using UrbanPlanToolbox.Models;
using Windows.ApplicationModel;
using Windows.Management.Deployment;

namespace UrbanPlanToolbox.Services;

/// <summary>GitHub channel updater. App Installer owns deployment; Releases API only supplies display metadata.</summary>
public sealed class GitHubAppUpdateService(GitHubUpdateService updateService) : IAppUpdateService
{
    private readonly GitHubUpdateService _updateService = updateService;
    private Version _localVersion = AppVersionProvider.GetCurrentVersion();
    private Uri? _appInstallerUri;
    private UpdateInstallSource _source;
    private bool _updateAvailable;

    public async Task<AppUpdateInfo> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        _localVersion = AppVersionProvider.GetCurrentVersion();
        if (!TryGetAppInstallerAssociation(out var package, out var installerInfo))
        {
            _source = package is null ? UpdateInstallSource.Unknown : UpdateInstallSource.LegacyGitHub;
            return await CheckLegacyAsync(cancellationToken);
        }

        _source = UpdateInstallSource.AppInstaller;
        _appInstallerUri = installerInfo!.Uri;
        try
        {
            var current = new PackageManager().FindPackageForUser(string.Empty, package!.Id.FullName);
            var availability = await current.CheckUpdateAvailabilityAsync().AsTask(cancellationToken);
            var metadata = await TryGetReleaseMetadataAsync(cancellationToken);
            switch (availability.Availability)
            {
                case PackageUpdateAvailability.Available:
                case PackageUpdateAvailability.Required:
                    _updateAvailable = true;
                    return new(AppUpdateState.UpdateAvailable, metadata.Version ?? installerInfo.Version.ToString(), metadata.Notes,
                        Source: _source, ReleaseNotes: metadata.Notes);
                case PackageUpdateAvailability.NoUpdates:
                    _updateAvailable = false;
                    return new(AppUpdateState.UpToDate, metadata.Version ?? _localVersion.ToString(), metadata.Notes,
                        Source: _source, ReleaseNotes: metadata.Notes);
                case PackageUpdateAvailability.Unknown:
                    return new(AppUpdateState.Failed, ErrorCode: "AppInstallerUnavailable", Source: _source);
                default:
                    return new(AppUpdateState.Failed, ErrorCode: "AppInstallerCheckFailed", Source: _source);
            }
        }
        catch (OperationCanceledException) { return new(AppUpdateState.Cancelled, Source: _source); }
        catch (COMException exception)
        {
            AppLogger.Default.Error("GitHubUpdate", "AppInstallerCheckFailed", exception, "App Installer update check failed.");
            return new(AppUpdateState.Failed, ErrorCode: $"0x{exception.HResult:X8}", Source: _source);
        }
        catch (Exception exception)
        {
            AppLogger.Default.Error("GitHubUpdate", "AppInstallerCheckFailed", exception, "App Installer update check failed.");
            return new(AppUpdateState.Failed, ErrorCode: "AppInstallerCheckFailed", Source: _source);
        }
    }

    public async Task<AppUpdateResult> DownloadAndInstallAsync(IProgress<AppUpdateProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (_source == UpdateInstallSource.LegacyGitHub) return new(AppUpdateState.UnsupportedChannel, "LegacyMigrationRequired");
        if (_source != UpdateInstallSource.AppInstaller || _appInstallerUri is null) return new(AppUpdateState.UnsupportedChannel, "AppInstallerUnavailable");
        if (!_updateAvailable) return new(AppUpdateState.Failed, "NoPendingUpdate");

        try
        {
            var restart = ApplicationRestartRegistration.Register();
            var manager = new PackageManager();
            var deploymentProgress = new Progress<DeploymentProgress>(value =>
            {
                var state = value.state == DeploymentProgressState.Queued ? AppUpdateState.Downloading : AppUpdateState.Installing;
                double? percentage = value.percentage is >= 0 and <= 100 ? value.percentage / 100d : null;
                progress?.Report(new(state, percentage));
            });
            progress?.Report(new(AppUpdateState.Downloading));
            var operation = manager.RequestAddPackageByAppInstallerFileAsync(_appInstallerUri, AddPackageByAppInstallerOptions.None, null);
            var result = await operation.AsTask(cancellationToken, deploymentProgress);
            if (result.IsRegistered) { progress?.Report(new(AppUpdateState.Restarting)); return new(AppUpdateState.Restarting); }
            restart.Dispose();
            return new(AppUpdateState.Failed, "InstallFailed");
        }
        catch (OperationCanceledException) { return new(AppUpdateState.Cancelled); }
        catch (COMException exception)
        {
            AppLogger.Default.Error("GitHubUpdate", "AppInstallerInstallFailed", exception, "App Installer update installation failed.");
            return new(AppUpdateState.Failed, $"0x{exception.HResult:X8}");
        }
        catch (Exception exception)
        {
            AppLogger.Default.Error("GitHubUpdate", "AppInstallerInstallFailed", exception, "App Installer update installation failed.");
            return new(AppUpdateState.Failed, "InstallFailed");
        }
    }

    private async Task<AppUpdateInfo> CheckLegacyAsync(CancellationToken cancellationToken)
    {
        var result = await _updateService.CheckForUpdatesAsync(_localVersion, cancellationToken);
        return result.Status switch
        {
            UpdateCheckStatus.UpdateAvailable => new(AppUpdateState.UpdateAvailable, result.RemoteVersion?.ToString(), result.Release?.Body,
                Source: _source, ReleaseNotes: result.Release?.Body),
            UpdateCheckStatus.UpToDate or UpdateCheckStatus.LocalVersionNewer => new(AppUpdateState.UpToDate, result.RemoteVersion?.ToString(), Source: _source),
            UpdateCheckStatus.NoRelease => new(AppUpdateState.Failed, ErrorCode: "NoRelease", Source: _source),
            UpdateCheckStatus.InvalidRemoteVersion or UpdateCheckStatus.InvalidResponse => new(AppUpdateState.Failed, ErrorCode: "InvalidResponse", Source: _source),
            _ => new(AppUpdateState.Failed, ErrorCode: "NetworkError", Source: _source)
        };
    }

    private async Task<(string? Version, string? Notes)> TryGetReleaseMetadataAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _updateService.CheckForUpdatesAsync(_localVersion, cancellationToken);
            return result.Status is UpdateCheckStatus.UpdateAvailable or UpdateCheckStatus.UpToDate or UpdateCheckStatus.LocalVersionNewer
                ? (result.RemoteVersion?.ToString(), result.Release?.Body)
                : (null, null);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            AppLogger.Default.Warning("GitHubUpdate", "ReleaseMetadataUnavailable", exception.Message);
            return (null, null);
        }
    }

    private static bool TryGetAppInstallerAssociation(out Package? package, out AppInstallerInfo? installerInfo)
    {
        package = null;
        installerInfo = null;
        try
        {
            package = Package.Current;
            installerInfo = package.GetAppInstallerInfo();
            if (installerInfo is not null)
            {
                AppLogger.Default.Info("GitHubUpdate", "AppInstallerAssociationDetected", $"Version={installerInfo.Version}; Uri={installerInfo.Uri}");
            }
            return installerInfo is not null;
        }
        catch (Exception exception)
        {
            AppLogger.Default.Warning("GitHubUpdate", "AppInstallerAssociationUnavailable", exception.Message);
            return false;
        }
    }
}

internal static class ApplicationRestartRegistration
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int RegisterApplicationRestart(string? commandLine, uint flags);

    public static IDisposable Register()
    {
        var result = RegisterApplicationRestart(null, 0);
        if (result < 0) throw new COMException("RegisterApplicationRestart failed.", result);
        return new Registration();
    }

    private sealed class Registration : IDisposable
    {
        public void Dispose() => RegisterApplicationRestart(string.Empty, 0);
    }
}
