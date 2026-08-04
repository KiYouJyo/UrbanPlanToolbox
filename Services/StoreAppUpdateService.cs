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

    public async Task<AppUpdateInfo> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        if (_channelService.GetCurrentChannel() != DistributionChannel.Store) return new(AppUpdateState.UnsupportedChannel);
        try
        {
            var context = CreateContext();
            _updates = await context.GetAppAndOptionalStorePackageUpdatesAsync().AsTask(cancellationToken);
            return _updates.Count == 0 ? new(AppUpdateState.UpToDate) : new(AppUpdateState.UpdateAvailable);
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
            var operation = CreateContext().RequestDownloadAndInstallStorePackageUpdatesAsync(_updates);
            operation.Progress = (_, status) =>
            {
                var amount = Math.Clamp(status.PackageDownloadProgress, 0d, 1d);
                progress?.Report(new(amount < 0.8 ? AppUpdateState.Downloading : AppUpdateState.Installing, amount));
            };
            var result = await operation.AsTask(cancellationToken);
            var state = result.OverallState.ToString();
            if (state.Contains("Cancel", StringComparison.OrdinalIgnoreCase)) return new(AppUpdateState.Cancelled);
            if (!state.Contains("Complete", StringComparison.OrdinalIgnoreCase)) return new(AppUpdateState.Failed, state);
            return new(AppUpdateState.Completed);
        }
        catch (OperationCanceledException) { return new(AppUpdateState.Cancelled); }
        catch (InvalidOperationException exception) when (exception.Message == "StoreWindowUnavailable") { return new(AppUpdateState.Failed, exception.Message); }
        catch (COMException exception) { return new(AppUpdateState.Failed, $"0x{exception.HResult:X8}"); }
        catch (Exception) { return new(AppUpdateState.Failed, "StoreInstallFailed"); }
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
