namespace UrbanPlanToolbox.Services;

public sealed record ShellNavigationState(
    string? PrimaryNavigationId,
    string? PageTypeName,
    bool IsSettings);

public sealed class NavigationStateService : INavigationStateService
{
    private ShellNavigationState? _state;

    public ShellNavigationState Capture() => _state ?? new(null, null, false);
    public void Save(ShellNavigationState state) => _state = state;
    public ShellNavigationState? Restore() => _state;
}
