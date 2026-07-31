namespace UrbanPlanToolbox.Models.Navigation;

public sealed record PrimaryNavigationDefinition(
    string Id,
    string DisplayName,
    Type PageType);
