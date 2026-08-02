namespace UrbanPlanToolbox.Models;

public sealed record RegulationListItem(
    RegulationEntry Entry,
    string Title,
    string Metadata,
    string Summary,
    string AutomationName);

public sealed class OfficialPortalListItem
{
    public required OfficialPortal Portal { get; init; }
    public bool IsValidUrl { get; init; }
    public required string UrlDisplay { get; init; }
    public required string AutomationName { get; init; }
    public required string OpenButtonText { get; init; }
    public required string OpenButtonToolTip { get; init; }
    public required string StatusText { get; init; }
}
