namespace UrbanPlanToolbox.Services;

public interface IApplicationRestartService
{
    bool TryRestart();
    bool TryRestart(out string? failureReason);
}
