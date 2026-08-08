namespace UrbanPlanToolbox.Models;

public sealed record AppUpdateProgress(AppUpdateState State, double? Value = null, string? Detail = null)
{
    public static double? NormalizeValue(double? value) => value is null ? null : double.IsFinite(value.Value) ? Math.Clamp(value.Value, 0d, 1d) : null;
}
