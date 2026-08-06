namespace UrbanPlanToolbox.Services;

public interface INavigationStateService
{
    ShellNavigationState Capture();
    void Save(ShellNavigationState state);
    ShellNavigationState? Restore();
}
