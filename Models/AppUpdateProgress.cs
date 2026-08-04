namespace UrbanPlanToolbox.Models;

public sealed record AppUpdateProgress(AppUpdateState State, double? Value = null, string? Detail = null);
