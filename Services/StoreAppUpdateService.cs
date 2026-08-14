using System.Runtime.InteropServices;
using UrbanPlanToolbox.Models;
using Windows.Services.Store;

namespace UrbanPlanToolbox.Services;

/// <summary>Store-only update provider. It never opens a Store page or downloads a GitHub package.</summary>
public sealed class StoreAppUpdateService(AppDistributionChannelService channelService, Func<nint?> windowHandleProvider, UpdateManifestService? manifestService = null) : IAppUpdateService
{
    private readonly AppDistributionChannelService _channelService = channelService;
    private readonly Func<nint?> _windowHandleProvider = windowHandleProvider;
    private readonly UpdateManifestService _manifestService = manifestService ?? UpdateManifestService.Default;
    private IReadOnlyList<StorePackageUpdate> _updates = Array.Empty<StorePackageUpdate>();
    private double? _lastDownloadProgress;
    private StorePackageUpdateState? _lastLoggedState;
    private int _lastLoggedProgressBucket = -1;

    public async Task<AppUpdateInfo> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        if (_channelService.GetCurrentChannel() != DistributionChannel.Store) return new(AppUpdateState.UnsupportedChannel);
        try
        {
            var context = CreateContext();
            _updates = await context.GetAppAndOptionalStorePackageUpdatesAsync().AsTask(cancellationToken);
            if (_updates.Count == 0) return new(AppUpdateState.UpToDate, AvailableVersion: AppVersionProvider.Version, Source: UpdateInstallSource.Store);
            var versionText = await _manifestService.GetVersionAsync(DistributionChannel.Store, cancellationToken);
            return new(AppUpdateState.UpdateAvailable, versionText, Source: UpdateInstallSource.Store);
        }
        catch (OperationCanceledException) { return new(AppUpdateState.Cancelled); }
        catch (InvalidOperationException exception) when (exception.Message == "StoreWindowUnavailable") { return new(AppUpdateState.Failed, exception.Message); }
        catch (COMException exception) { return new(AppUpdateState.Failed, $"0x{exception.HResult:X8}"); }
        catch (Exception) { return new(AppUpdateState.Failed, "StoreCheckFailed"); }
    }

    public async Task<AppUpdateResult> DownloadAndInstallAsync(IProgress<AppUpdateProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (_channelService.GetCurrentChannel() != DistributionChannel.Store) return new(AppUpdateState.UnsupportedChannel);
        if (_updates.Count == 0) return new(AppUpdateState.Failed, "NoPendingUpdate");
        try
        {
            ResetProgressTracking();
            // This is intentionally download-only. A Store package must not be deployed
            // until the user explicitly chooses the second, restart-and-update action.
            var operation = CreateContext().RequestDownloadStorePackageUpdatesAsync(_updates);
            var storeProgress = new Progress<StorePackageUpdateStatus>(status => HandleStoreProgress(status, progress, isInstallOperation: false));
            var result = await operation.AsTask(cancellationToken, storeProgress);
            var state = result.OverallState.ToString();
            if (state.Equals("Canceled", StringComparison.OrdinalIgnoreCase) || state.Equals("Cancelled", StringComparison.OrdinalIgnoreCase)) return new(AppUpdateState.Cancelled);
            if (state.Equals("Completed", StringComparison.OrdinalIgnoreCase))
            {
                AppLogger.Default.Info("StoreUpdate", "StoreDownloadCompleted", "StoreOverallState=Completed;MappedAppState=ReadyToInstall");
                return new(AppUpdateState.ReadyToInstall);
            }
            return new(AppUpdateState.Failed, $"StoreDownload{state}");
        }
        catch (OperationCanceledException) { return new(AppUpdateState.Cancelled); }
        catch (InvalidOperationException exception) when (exception.Message == "StoreWindowUnavailable") { return new(AppUpdateState.Failed, exception.Message); }
        catch (COMException exception) { return new(AppUpdateState.Failed, $"0x{exception.HResult:X8}"); }
        catch (Exception) { return new(AppUpdateState.Failed, "StoreDownloadFailed"); }
    }

    public async Task<AppUpdateResult> InstallPendingAsync(IProgress<AppUpdateProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (_channelService.GetCurrentChannel() != DistributionChannel.Store) return new(AppUpdateState.UnsupportedChannel);
        if (_updates.Count == 0) return new(AppUpdateState.Failed, "NoPendingUpdate");
        try
        {
            ResetProgressTracking();
            var operation = CreateContext().RequestDownloadAndInstallStorePackageUpdatesAsync(_updates);
            var storeProgress = new Progress<StorePackageUpdateStatus>(status => HandleStoreProgress(status, progress, isInstallOperation: true));
            var result = await operation.AsTask(cancellationToken, storeProgress);
            var state = result.OverallState.ToString();
            if (state.Equals("Canceled", StringComparison.OrdinalIgnoreCase) || state.Equals("Cancelled", StringComparison.OrdinalIgnoreCase)) return new(AppUpdateState.Cancelled);
            if (state.Equals("Completed", StringComparison.OrdinalIgnoreCase))
            {
                // Store owns package deployment and may terminate the running process.
                // The coordinator has already arranged relaunch before deployment begins.
                AppLogger.Default.Info("StoreUpdate", "StoreInstallCompleted", "StoreOverallState=Completed;MappedAppState=Completed");
                return new(AppUpdateState.Completed);
            }
            return new(AppUpdateState.Failed, $"StoreInstall{state}");
        }
        catch (OperationCanceledException) { return new(AppUpdateState.Cancelled); }
        catch (InvalidOperationException exception) when (exception.Message == "StoreWindowUnavailable") { return new(AppUpdateState.Failed, exception.Message); }
        catch (COMException exception) { return new(AppUpdateState.Failed, $"0x{exception.HResult:X8}"); }
        catch (Exception) { return new(AppUpdateState.Failed, "StoreInstallFailed"); }
    }

    private void ResetProgressTracking()
    {
        _lastDownloadProgress = null;
        _lastLoggedState = null;
        _lastLoggedProgressBucket = -1;
    }

    private void HandleStoreProgress(StorePackageUpdateStatus status, IProgress<AppUpdateProgress>? progress, bool isInstallOperation)
    {
        var updateState = status.PackageUpdateState;
        var resolution = StoreUpdateProgressResolver.ResolveDownloadProgress(
            status.TotalDownloadProgress,
            status.PackageDownloadProgress,
            status.PackageBytesDownloaded,
            status.PackageDownloadSizeInBytes);
        switch (updateState)
        {
            case StorePackageUpdateState.Pending:
            case StorePackageUpdateState.Downloading:
                if (resolution.Value is double currentProgress)
                    _lastDownloadProgress = Math.Max(_lastDownloadProgress ?? 0d, currentProgress);
                progress?.Report(new(AppUpdateState.Downloading, _lastDownloadProgress));
                LogProgress(status, AppUpdateState.Downloading, _lastDownloadProgress, resolution.Source);
                break;
            case StorePackageUpdateState.Deploying:
                // A deployment callback is only meaningful after the user chose install.
                // It is activity, not a transaction terminal state.
                if (isInstallOperation) progress?.Report(new(AppUpdateState.Installing));
                LogProgress(status, AppUpdateState.Installing, null, "None");
                break;
            case StorePackageUpdateState.Completed:
                // Per-package completion must never advance the application state. The
                // awaited StorePackageUpdateResult.OverallState is authoritative.
                LogProgress(status, isInstallOperation ? AppUpdateState.Installing : AppUpdateState.Downloading, _lastDownloadProgress, "PackageCompleted");
                break;
            case StorePackageUpdateState.Canceled:
                LogProgress(status, isInstallOperation ? AppUpdateState.Installing : AppUpdateState.Downloading, _lastDownloadProgress, "PackageCancelled");
                break;
            case StorePackageUpdateState.OtherError:
            case StorePackageUpdateState.ErrorLowBattery:
            case StorePackageUpdateState.ErrorWiFiRecommended:
            case StorePackageUpdateState.ErrorWiFiRequired:
                LogProgress(status, isInstallOperation ? AppUpdateState.Installing : AppUpdateState.Downloading, _lastDownloadProgress, $"Package{updateState}");
                break;
            default:
                progress?.Report(new(AppUpdateState.Downloading, _lastDownloadProgress));
                LogProgress(status, AppUpdateState.Downloading, _lastDownloadProgress, "LastValid");
                break;
        }
    }

    private void LogProgress(StorePackageUpdateStatus status, AppUpdateState mappedState, double? uiProgress, string source)
    {
        var bucket = uiProgress is double value ? (int)Math.Floor(value * 20d) : -1;
        if (_lastLoggedState == status.PackageUpdateState && bucket == _lastLoggedProgressBucket) return;
        _lastLoggedState = status.PackageUpdateState;
        _lastLoggedProgressBucket = bucket;
        var message = $"AppVersion={AppVersionProvider.Version};StoreState={status.PackageUpdateState};PackageDownloadProgress={status.PackageDownloadProgress:0.###};TotalDownloadProgress={status.TotalDownloadProgress:0.###};PackageBytesDownloaded={status.PackageBytesDownloaded};PackageDownloadSizeInBytes={status.PackageDownloadSizeInBytes};MappedAppState={mappedState};MappedUiProgress={(uiProgress?.ToString("0.###") ?? "null")};ProgressSource={source};PackageFamilyName={status.PackageFamilyName}";
        AppLogger.Default.Info("StoreUpdate", "StoreUpdateProgress", message);
    }

    private StoreContext CreateContext()
    {
        var windowHandle = _windowHandleProvider();
        if (windowHandle is null || windowHandle == nint.Zero) throw new InvalidOperationException("StoreWindowUnavailable");
        var context = StoreContext.GetDefault();
        WinRT.Interop.InitializeWithWindow.Initialize(context, windowHandle.Value);
        return context;
    }
}
