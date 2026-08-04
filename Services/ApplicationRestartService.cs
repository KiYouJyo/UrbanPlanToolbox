using Microsoft.Windows.AppLifecycle;

namespace UrbanPlanToolbox.Services;

public sealed class ApplicationRestartService : IApplicationRestartService
{
    public bool TryRestart()
    {
        try { return string.Equals(AppInstance.Restart(string.Empty).ToString(), "RestartPending", StringComparison.Ordinal); }
        catch (Exception) { return false; }
    }
}
