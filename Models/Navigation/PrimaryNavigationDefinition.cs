namespace UrbanPlanToolbox.Models.Navigation;

public sealed record PrimaryNavigationDefinition(
    string Id,
    string NameResourceKey,
    Type PageType);
