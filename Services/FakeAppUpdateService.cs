using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.Services;

public enum FakeAppUpdateScenario { UpToDate, UpdateAvailable, Cancelled, NetworkError, StoreUnavailable, DownloadFailed, InstallFailed, UnsupportedChannel, InstallWillCloseApp }

public sealed class FakeAppUpdateService(FakeAppUpdateScenario scenario = FakeAppUpdateScenario.UpToDate) : IAppUpdateService
{
    private readonly FakeAppUpdateScenario _scenario = scenario;
    public Task<AppUpdateInfo> CheckForUpdatesAsync(CancellationToken cancellationToken = default) => Task.FromResult(_scenario switch
    {
        FakeAppUpdateScenario.UpToDate => new AppUpdateInfo(AppUpdateState.UpToDate),
        FakeAppUpdateScenario.UpdateAvailable or FakeAppUpdateScenario.Cancelled or FakeAppUpdateScenario.DownloadFailed or FakeAppUpdateScenario.InstallFailed or FakeAppUpdateScenario.InstallWillCloseApp => new AppUpdateInfo(AppUpdateState.UpdateAvailable, "1.2.1"),
        FakeAppUpdateScenario.UnsupportedChannel => new AppUpdateInfo(AppUpdateState.UnsupportedChannel),
        FakeAppUpdateScenario.NetworkError => new AppUpdateInfo(AppUpdateState.Failed, ErrorCode: "NetworkError"),
        _ => new AppUpdateInfo(AppUpdateState.Failed, ErrorCode: "StoreUnavailable")
    });
    public async Task<AppUpdateResult> DownloadAndInstallAsync(IProgress<AppUpdateProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (_scenario == FakeAppUpdateScenario.UnsupportedChannel) return new(AppUpdateState.UnsupportedChannel);
        if (_scenario == FakeAppUpdateScenario.Cancelled) return new(AppUpdateState.Cancelled);
        if (_scenario == FakeAppUpdateScenario.DownloadFailed) return new(AppUpdateState.Failed, "DownloadFailed");
        progress?.Report(new(AppUpdateState.Downloading, 0)); await Task.Yield(); cancellationToken.ThrowIfCancellationRequested();
        progress?.Report(new(AppUpdateState.Downloading, 0.5)); await Task.Yield();
        if (_scenario == FakeAppUpdateScenario.InstallFailed) return new(AppUpdateState.Failed, "InstallFailed");
        progress?.Report(new(AppUpdateState.Installing, 1));
        return new(_scenario == FakeAppUpdateScenario.InstallWillCloseApp ? AppUpdateState.Installing : AppUpdateState.Completed);
    }
}
