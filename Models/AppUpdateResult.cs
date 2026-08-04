namespace UrbanPlanToolbox.Models;

public sealed record AppUpdateResult(AppUpdateState State, string? ErrorCode = null, string? Detail = null);
