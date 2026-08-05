using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.Services;

/// <summary>Adapts the existing GitHub release checker to the About-page update state model.</summary>
public sealed class GitHubAppUpdateService(GitHubUpdateService updateService) : IAppUpdateService
{
    private readonly GitHubUpdateService _updateService = updateService;
    private Version _localVersion = AppVersionProvider.GetCurrentVersion();

    public async Task<AppUpdateInfo> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        var result = await _updateService.CheckForUpdatesAsync(_localVersion, cancellationToken);
        return result.Status switch
        {
            UpdateCheckStatus.UpdateAvailable => new(AppUpdateState.UpdateAvailable, result.RemoteVersion?.ToString(), result.Release?.HtmlUrl.ToString()),
            UpdateCheckStatus.UpToDate or UpdateCheckStatus.LocalVersionNewer => new(AppUpdateState.UpToDate, result.RemoteVersion?.ToString()),
            UpdateCheckStatus.NoRelease => new(AppUpdateState.Failed, ErrorCode: "NoRelease"),
            UpdateCheckStatus.ConnectionFailed => new(AppUpdateState.Failed, ErrorCode: "NetworkError"),
            UpdateCheckStatus.TimedOut => new(AppUpdateState.Failed, ErrorCode: "NetworkError"),
            UpdateCheckStatus.RateLimited => new(AppUpdateState.Failed, ErrorCode: "NetworkError"),
            UpdateCheckStatus.InvalidRemoteVersion => new(AppUpdateState.Failed, ErrorCode: "InvalidResponse"),
            _ => new(AppUpdateState.Failed, ErrorCode: "NetworkError")
        };
    }

    public Task<AppUpdateResult> DownloadAndInstallAsync(IProgress<AppUpdateProgress>? progress = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(new AppUpdateResult(AppUpdateState.UnsupportedChannel, "GitHubManualInstall"));
}
