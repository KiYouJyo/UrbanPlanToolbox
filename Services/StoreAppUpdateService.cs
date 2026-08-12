using System.Runtime.InteropServices;
using UrbanPlanToolbox.Models;
using Windows.Services.Store;

namespace UrbanPlanToolbox.Services;

/// <summary>Store-only update provider. It never opens a Store page or downloads a GitHub package.</summary>
public sealed class StoreAppUpdateService(AppDistributionChannelService channelService, Func<nint?> windowHandleProvider) : IAppUpdateService
{
    private readonly AppDistributionChannelService _channelService = channelService;
    private readonly Func<nint?> _windowHandleProvider = windowHandleProvider;
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
            if (_updates.Count == 0) return new(AppUpdateState.UpToDate, Source: UpdateInstallSource.Unknown);
            var version = _updates[0].Package?.Id?.Version;
            var versionText = version is null ? null : $"{version.Value.Major}.{version.Value.Minor}.{version.Value.Build}";
            return new(AppUpdateState.UpdateAvailable, versionText, Source: UpdateInstallSource.Unknown);
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
            _lastDownloadProgress = null;
            _lastLoggedState = null;
            _lastLoggedProgressBucket = -1;
            var operation = CreateContext().RequestDownloadAndInstallStorePackageUpdatesAsync(_updates);
            var storeProgress = new Progress<StorePackageUpdateStatus>(status =>
            {
                HandleStoreProgress(status, progress);
            });
            var result = await operation.AsTask(cancellationToken, storeProgress);
            var state = result.OverallState.ToString();
            if (state.Equals("Canceled", StringComparison.OrdinalIgnoreCase) || state.Equals("Cancelled", StringComparison.OrdinalIgnoreCase)) return new(AppUpdateState.Cancelled);
            if (state.Equals("Completed", StringComparison.OrdinalIgnoreCase)) return new(AppUpdateState.Completed);
            return new(AppUpdateState.Failed, state);
        }
        catch (OperationCanceledException) { return new(AppUpdateState.Cancelled); }
        catch (InvalidOperationException exception) when (exception.Message == "StoreWindowUnavailable") { return new(AppUpdateState.Failed, exception.Message); }
        catch (COMException exception) { return new(AppUpdateState.Failed, $"0x{exception.HResult:X8}"); }
        catch (Exception) { return new(AppUpdateState.Failed, "StoreInstallFailed"); }
    }

    private void HandleStoreProgress(StorePackageUpdateStatus status, IProgress<AppUpdateProgress>? progress)
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
                progress?.Report(new(AppUpdateState.Installing));
                LogProgress(status, AppUpdateState.Installing, null, "None");
                break;
            case StorePackageUpdateState.Completed:
                progress?.Report(new(AppUpdateState.Completed, 1d));
                LogProgress(status, AppUpdateState.Completed, 1d, "Total");
                break;
            case StorePackageUpdateState.Canceled:
                progress?.Report(new(AppUpdateState.Cancelled));
                LogProgress(status, AppUpdateState.Cancelled, null, "None");
                break;
            case StorePackageUpdateState.OtherError:
            case StorePackageUpdateState.ErrorLowBattery:
            case StorePackageUpdateState.ErrorWiFiRecommended:
            case StorePackageUpdateState.ErrorWiFiRequired:
                progress?.Report(new(AppUpdateState.Failed, Detail: updateState.ToString()));
                LogProgress(status, AppUpdateState.Failed, null, "None");
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
