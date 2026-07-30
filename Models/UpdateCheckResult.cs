namespace UrbanPlanToolbox.Models;

public enum UpdateCheckStatus
{
    UpToDate,
    UpdateAvailable,
    LocalVersionNewer,
    NoRelease,
    ConnectionFailed,
    TimedOut,
    RateLimited,
    InvalidResponse,
    InvalidRemoteVersion,
    RequestFailed
}

public sealed record UpdateCheckResult(UpdateCheckStatus Status, Version LocalVersion, Version? RemoteVersion = null, GitHubRelease? Release = null);
