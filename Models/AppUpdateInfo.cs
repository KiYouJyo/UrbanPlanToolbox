namespace UrbanPlanToolbox.Models;

public enum UpdateInstallSource { Unknown, GitHub, Store, AppInstaller, LegacyGitHub }

public sealed record AppUpdateInfo(
    AppUpdateState State,
    string? AvailableVersion = null,
    string? Detail = null,
    string? ErrorCode = null,
    string? ReleaseNotes = null,
    UpdateInstallSource Source = UpdateInstallSource.Unknown,
    LocalizedReleaseNotes? LocalizedReleaseNotes = null)
{
    public bool IsUpdateAvailable => State == AppUpdateState.UpdateAvailable;
    public bool IsReadyToInstall => State == AppUpdateState.ReadyToInstall;
}
