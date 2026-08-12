using System.Runtime.InteropServices;
using UrbanPlanToolbox.Models;
using Windows.Management.Deployment;

namespace UrbanPlanToolbox.Services;

/// <summary>GitHub channel updater. Releases API supplies assets; deployment always uses a verified local bundle.</summary>
public sealed class GitHubAppUpdateService(GitHubUpdateService updateService) : IAppUpdateService
{
    private readonly GitHubUpdateService _updateService = updateService;
    private Version _localVersion = AppVersionProvider.GetCurrentVersion();
    private GitHubRelease? _pendingRelease;
    private bool _updateAvailable;

    public async Task<AppUpdateInfo> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        _localVersion = AppVersionProvider.GetCurrentVersion();
        var result = await _updateService.CheckForUpdatesAsync(_localVersion, cancellationToken);
        return result.Status switch
        {
            UpdateCheckStatus.UpdateAvailable => SetPending(result, new(AppUpdateState.UpdateAvailable, result.RemoteVersion?.ToString(), result.Release?.Body,
                Source: UpdateInstallSource.GitHub, ReleaseNotes: result.Release?.Body)),
            UpdateCheckStatus.UpToDate or UpdateCheckStatus.LocalVersionNewer => SetPending(result, new(AppUpdateState.UpToDate, result.RemoteVersion?.ToString(), result.Release?.Body,
                Source: UpdateInstallSource.GitHub, ReleaseNotes: result.Release?.Body)),
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
            var bundleName = $"UrbanPlanToolbox_{_pendingRelease.TagName.TrimStart('v')}.0_x64.msixbundle";
            var bundlePath = await _updateService.DownloadAndVerifyBundleAsync(_pendingRelease, bundleName, progress, cancellationToken);
            if (bundlePath is null) return new(AppUpdateState.Failed, "BundleVerificationFailed");

            progress?.Report(new(AppUpdateState.Installing));
            using var restart = ApplicationRestartRegistration.Register();
            var manager = new PackageManager();
            var operation = manager.AddPackageAsync(new Uri(bundlePath), null, DeploymentOptions.ForceApplicationShutdown);
            var deploymentProgress = new Progress<DeploymentProgress>(value =>
            {
                var state = value.state == DeploymentProgressState.Queued ? AppUpdateState.Downloading : AppUpdateState.Installing;
                double? percentage = value.percentage is >= 0 and <= 100 ? value.percentage / 100d : null;
                progress?.Report(new(state, percentage, $"Deployment {value.percentage}%"));
            });
            var result = await operation.AsTask(cancellationToken, deploymentProgress);
            if (!result.IsRegistered)
            {
                AppLogger.Default.Error("GitHubUpdate", "PackageDeploymentFailed", null, $"Error={result.ErrorText}; ExtendedError={result.ExtendedErrorCode}");
                return new(AppUpdateState.Failed, "PackageDeploymentFailed");
            }
            AppLogger.Default.Info("GitHubUpdate", "PackageDeploymentCompleted", $"Version={_pendingRelease.TagName}; Bundle={Path.GetFileName(bundlePath)}");
            progress?.Report(new(AppUpdateState.Restarting));
            return new(AppUpdateState.Restarting);
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

    private AppUpdateInfo SetPending(UpdateCheckResult result, AppUpdateInfo info)
    {
        _pendingRelease = result.Release;
        _updateAvailable = result.Status == UpdateCheckStatus.UpdateAvailable;
        return info;
    }

    private static AppUpdateInfo Fail(string code) => new(AppUpdateState.Failed, ErrorCode: code, Source: UpdateInstallSource.GitHub);
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
