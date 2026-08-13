using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Security.Cryptography;
using Windows.ApplicationModel;
using Windows.Management.Deployment;
using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.Services;

/// <summary>GitHub channel updater. A verified local bundle is deployed by Windows package deployment.</summary>
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
            var bundleName = $"UrbanPlanToolbox_{_pendingRelease.TagName.TrimStart('v')}.0_x64.msixbundle";
            var bundlePath = await _updateService.DownloadAndVerifyBundleAsync(_pendingRelease, bundleName, progress, cancellationToken);
            if (bundlePath is null) return new(AppUpdateState.Failed, "BundleVerificationFailed");

            progress?.Report(new(AppUpdateState.Installing, Detail: "Verified; deployment queued"));
            var bundleInfo = new FileInfo(bundlePath);
            var bundleHash = Convert.ToHexString(await SHA256.HashDataAsync(File.OpenRead(bundlePath), cancellationToken));
            var current = Package.Current.Id;
            if (!VersionParser.TryParseTag(_pendingRelease.TagName, out var targetVersion)) return new(AppUpdateState.Failed, "InvalidRemoteVersion");
            var deploymentStarted = Stopwatch.GetTimestamp();
            AppLogger.Default.Info("GitHubUpdate", "DeploymentStarting", $"CurrentName={current.Name}; CurrentPackageFullName={current.FullName}; CurrentPackageFamilyName={current.FamilyName}; CurrentVersion={current.Version}; TargetVersion={targetVersion}; LocalBundlePath={bundlePath}; LocalBundleUri={new Uri(bundlePath).AbsoluteUri}; BundleSize={bundleInfo.Length}; BundleSHA256={bundleHash}; Publisher={current.Publisher}; DeploymentOptions={DeploymentOptions.ForceApplicationShutdown}; Timestamp={DateTimeOffset.UtcNow:O}");
            using var restart = ApplicationRestartRegistration.Register(out var restartHresult);
            AppLogger.Default.Info("GitHubUpdate", "RegisterApplicationRestart", $"HRESULT=0x{restartHresult:X8}; Succeeded={restartHresult == 0}");
            var manager = new PackageManager();
            var operation = manager.AddPackageAsync(new Uri(bundlePath), null, DeploymentOptions.ForceApplicationShutdown);
            var deploymentProgress = new Progress<DeploymentProgress>(value =>
            {
                var state = value.state == DeploymentProgressState.Queued ? AppUpdateState.Downloading : AppUpdateState.Installing;
                double? percentage = value.percentage is >= 0 and <= 100 ? value.percentage / 100d : null;
                progress?.Report(new(state, percentage, $"Deployment {value.percentage}%"));
            });
            var result = await operation.AsTask(cancellationToken, deploymentProgress);
            var elapsed = Stopwatch.GetElapsedTime(deploymentStarted);
            AppLogger.Default.Info("GitHubUpdate", "DeploymentResult", $"IsRegistered={result.IsRegistered}; ActivityId={result.ActivityId}; ExtendedErrorCode={result.ExtendedErrorCode}; ErrorText={result.ErrorText}; FinalState={(result.IsRegistered ? "Registered" : "Failed")}; ElapsedMs={elapsed.TotalMilliseconds:0}");
            if (!result.IsRegistered)
            {
                AppLogger.Default.Error("GitHubUpdate", "PackageDeploymentFailed", null, $"ActivityId={result.ActivityId}; Error={result.ErrorText}; ExtendedError={result.ExtendedErrorCode}");
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

internal static class ApplicationRestartRegistration
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int RegisterApplicationRestart(string? commandLine, uint flags);

    public static IDisposable Register(out int hresult)
    {
        var result = RegisterApplicationRestart(null, 0);
        hresult = result;
        if (result != 0) throw new COMException("RegisterApplicationRestart failed.", result);
        return new Registration();
    }

    private sealed class Registration : IDisposable
    {
        public void Dispose() => RegisterApplicationRestart(string.Empty, 0);
    }
}
