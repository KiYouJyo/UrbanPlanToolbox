using Windows.ApplicationModel;

namespace UrbanPlanToolbox.Services;

/// <summary>Uses the package-declared StartupTask; requesting enablement is always user initiated from Settings.</summary>
public sealed class WindowsStartupService
{
    public const string TaskId = "UrbanPlanToolboxBackgroundStartup";
    public static WindowsStartupService Default { get; } = new();
    public async Task<bool> IsEnabledAsync()
    {
        try { return (await StartupTask.GetAsync(TaskId)).State == StartupTaskState.Enabled; }
        catch (Exception) { return false; }
    }
    public async Task<bool> SetEnabledAsync(bool enabled)
    {
        try
        {
            var task = await StartupTask.GetAsync(TaskId);
            if (!enabled) { task.Disable(); return true; }
            return task.State == StartupTaskState.Enabled || await task.RequestEnableAsync() == StartupTaskState.Enabled;
        }
        catch (Exception) { return false; }
    }
}
