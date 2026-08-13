using System.Runtime.InteropServices;
using Windows.ApplicationModel;
using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.Services;

/// <summary>GitHub channel updater. Windows App Installer owns the final update confirmation and installation.</summary>
public sealed class GitHubAppUpdateService(GitHubUpdateService updateService, UpdateManifestService? manifestService = null) : IAppUpdateService
{
    private readonly GitHubUpdateService _updateService = updateService;
    private readonly UpdateManifestService _manifestService = manifestService ?? UpdateManifestService.Default;
    private Version _localVersion = AppVersionProvider.GetCurrentVersion();
    private GitHubRelease? _pendingRelease;
    private bool _updateAvailable;

    public async Task<AppUpdateInfo> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        _localVersion = AppVersionProvider.GetCurrentVersion();
        var result = await _updateService.CheckForUpdatesAsync(_localVersion, cancellationToken);
        return result.Status switch
        {
            UpdateCheckStatus.UpdateAvailable => await SetPendingAsync(result, cancellationToken),
            UpdateCheckStatus.UpToDate or UpdateCheckStatus.LocalVersionNewer => SetPending(result, new(AppUpdateState.UpToDate, AvailableVersion: GetRemoteVersion(result), Source: UpdateInstallSource.AppInstaller)),
            UpdateCheckStatus.NoRelease => Fail("ReleaseNotFound"),
            UpdateCheckStatus.InvalidRemoteVersion or UpdateCheckStatus.InvalidResponse => Fail("InvalidReleaseResponse"),
            UpdateCheckStatus.RateLimited => Fail("GitHubRateLimited"),
            UpdateCheckStatus.TimedOut or UpdateCheckStatus.ConnectionFailed or UpdateCheckStatus.RequestFailed => Fail("UnableToContactGitHub"),
            _ => Fail("GitHubCheckFailed")
        };
    }

    public async Task<AppUpdateResult> DownloadAndInstallAsync(IProgress<AppUpdateProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!_updateAvailable || _pendingRelease is null) return new(AppUpdateState.Failed, "NoPendingUpdate");
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new(AppUpdateState.Installing, Detail: "Opening Windows App Installer"));
            if (!await ExternalLinkService.OpenAsync(RepositoryLinks.AppInstaller.ToString())) return new(AppUpdateState.Failed, "AppInstallerUnavailable");
            AppLogger.Default.Info("GitHubUpdate", "AppInstallerLaunched", $"Uri={RepositoryLinks.AppInstaller}; TargetVersion={_pendingRelease.TagName}");
            return new(AppUpdateState.Completed, "Windows App Installer opened");
        }
        catch (OperationCanceledException) { return new(AppUpdateState.Cancelled, "Cancelled"); }
        catch (COMException exception)
        {
            AppLogger.Default.Error("GitHubUpdate", "PackageDeploymentFailed", exception, $"HRESULT=0x{exception.HResult:X8}");
            return new(AppUpdateState.Failed, $"0x{exception.HResult:X8}");
        }
        catch (Exception exception)
        {
            AppLogger.Default.Error("GitHubUpdate", "PackageDeploymentFailed", exception, exception.Message);
            return new(AppUpdateState.Failed, "PackageDeploymentFailed");
        }
    }

    private async Task<AppUpdateInfo> SetPendingAsync(UpdateCheckResult result, CancellationToken cancellationToken)
    {
        _pendingRelease = result.Release;
        _updateAvailable = result.Status == UpdateCheckStatus.UpdateAvailable;
        var displayVersion = GetRemoteVersion(result) ?? await _manifestService.GetVersionAsync(DistributionChannel.GitHub, cancellationToken);
        return new(AppUpdateState.UpdateAvailable, displayVersion, Source: UpdateInstallSource.AppInstaller);
    }

    private static AppUpdateInfo Fail(string code) => new(AppUpdateState.Failed, ErrorCode: code, Source: UpdateInstallSource.GitHub);

    private AppUpdateInfo SetPending(UpdateCheckResult result, AppUpdateInfo info)
    {
        _pendingRelease = result.Release;
        _updateAvailable = result.Status == UpdateCheckStatus.UpdateAvailable;
        return info;
    }

    private static string? GetRemoteVersion(UpdateCheckResult result) => result.RemoteVersion is { } version
        ? $"{version.Major}.{version.Minor}.{version.Build}"
        : null;
}
