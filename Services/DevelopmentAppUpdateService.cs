using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.Services;

/// <summary>Prevents unpackaged/debug builds from being mistaken for a release channel.</summary>
public sealed class DevelopmentAppUpdateService : IAppUpdateService
{
    public Task<AppUpdateInfo> CheckForUpdatesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new AppUpdateInfo(AppUpdateState.UnsupportedChannel, ErrorCode: "DevelopmentBuild"));

    public Task<AppUpdateResult> DownloadAndInstallAsync(IProgress<AppUpdateProgress>? progress = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(new AppUpdateResult(AppUpdateState.UnsupportedChannel, "DevelopmentBuild"));
}
