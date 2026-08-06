using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.Services;

public interface IFirstRunExperienceService
{
    int CurrentFirstRunGuideVersion { get; }
    FirstRunGuideInstallationState InstallationState { get; }
    bool ShouldShowAutomatically();
    bool IsCompleted { get; }
    bool TryMarkCompleted(out string? error);
}
