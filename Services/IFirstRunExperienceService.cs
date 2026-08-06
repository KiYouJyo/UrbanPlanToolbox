namespace UrbanPlanToolbox.Services;

public interface IFirstRunExperienceService
{
    int CurrentFirstRunGuideVersion { get; }
    bool ShouldShowAutomatically();
    bool IsCompleted { get; }
    bool TryMarkCompleted(out string? error);
}
