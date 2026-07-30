namespace UrbanPlanToolbox.Models;

public sealed record MeasurementUnit(
    string Id,
    string DisplayName,
    string Symbol,
    MeasurementCategory Category,
    string System,
    decimal Numerator,
    decimal Denominator,
    int SortOrder,
    bool IsTraditional = false,
    string? Note = null)
{
    public decimal ToBaseFactor => Numerator / Denominator;
}
