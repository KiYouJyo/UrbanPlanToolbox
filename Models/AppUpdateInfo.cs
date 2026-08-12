namespace UrbanPlanToolbox.Models;

public enum UpdateInstallSource { Unknown, AppInstaller, LegacyGitHub }

public sealed record AppUpdateInfo(
    AppUpdateState State,
    string? Version = null,
    string? Detail = null,
    string? ErrorCode = null,
    string? ReleaseNotes = null,
    UpdateInstallSource Source = UpdateInstallSource.Unknown)
{
    public bool IsUpdateAvailable => State == AppUpdateState.UpdateAvailable;
}
