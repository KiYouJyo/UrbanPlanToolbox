using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.Services;

public interface IAppUpdateService
{
    Task<AppUpdateInfo> CheckForUpdatesAsync(CancellationToken cancellationToken = default);
    Task<AppUpdateResult> DownloadAndInstallAsync(IProgress<AppUpdateProgress>? progress = null, CancellationToken cancellationToken = default);
}
