namespace UrbanPlanToolbox.Models;

public sealed record AppUpdateInfo(AppUpdateState State, string? Version = null, string? Detail = null, string? ErrorCode = null)
{
    public bool IsUpdateAvailable => State == AppUpdateState.UpdateAvailable;
}
