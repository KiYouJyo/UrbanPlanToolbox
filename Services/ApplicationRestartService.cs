using Microsoft.Windows.AppLifecycle;

namespace UrbanPlanToolbox.Services;

public sealed class ApplicationRestartService : IApplicationRestartService
{
    public bool TryRestart() => TryRestart(out _);

    public bool TryRestart(out string? failureReason)
    {
        failureReason = null;
        try
        {
            var result = AppInstance.Restart(string.Empty);
            failureReason = result.ToString();
            AppLogger.Default.Info("ApplicationRestart", "RestartReturned", $"AppInstance.Restart returned={failureReason}");
            return false;
        }
        catch (Exception exception)
        {
            failureReason = exception.Message;
            AppLogger.Default.Error("ApplicationRestart", "RestartFailed", exception, $"FailureReason={failureReason}");
            return false;
        }
    }
}
