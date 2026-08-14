using System.Runtime.InteropServices;

namespace UrbanPlanToolbox.Services;

/// <summary>Thin, process-scoped wrapper around the Windows Application Restart APIs.</summary>
public sealed class ApplicationRestartRegistrationService : IApplicationRestartRegistrationService
{
    // Zero opts into restart after an update while avoiding application-specific command-line data.
    private const uint RestartAfterUpdateFlags = 0;

    public bool TryRegister(out string? failureReason)
    {
        AppLogger.Default.Info("StoreUpdate", "RelaunchRegistrationRequested", "CommandLine=None;Flags=0");
        var result = RegisterApplicationRestart(null, RestartAfterUpdateFlags);
        if (result >= 0)
        {
            failureReason = null;
            AppLogger.Default.Info("StoreUpdate", "RelaunchRegistrationSucceeded", "HResult=S_OK");
            return true;
        }

        failureReason = $"0x{result:X8}";
        AppLogger.Default.Info("StoreUpdate", "RelaunchRegistrationFailed", $"HResult={failureReason}");
        return false;
    }

    public void Unregister()
    {
        var result = UnregisterApplicationRestart();
        AppLogger.Default.Info("StoreUpdate", "RelaunchRegistrationRemoved", $"HResult=0x{result:X8}");
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int RegisterApplicationRestart(string? commandLine, uint flags);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern int UnregisterApplicationRestart();
}
