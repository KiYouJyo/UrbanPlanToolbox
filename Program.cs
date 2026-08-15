using Microsoft.UI.Xaml;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.AppLifecycle;
using UrbanPlanToolbox.Services;
using WinRT;

namespace UrbanPlanToolbox;

/// <summary>Owns process startup so instance arbitration occurs before WinUI is initialized.</summary>
public static class Program
{
    public static bool IsBackgroundStartup { get; private set; }
    [STAThread]
    public static void Main(string[] args)
    {
        ComWrappersSupport.InitializeComWrappers();

        var currentInstance = AppInstance.GetCurrent();
        StartupTiming.Default.Mark($"T0 Process entry; activation={currentInstance.GetActivatedEventArgs().Kind}");
        IsBackgroundStartup = args.Any(argument => string.Equals(argument, "--background-startup", StringComparison.OrdinalIgnoreCase)) || string.Equals(currentInstance.GetActivatedEventArgs().Kind.ToString(), "StartupTask", StringComparison.Ordinal);
        var mainInstance = AppInstance.FindOrRegisterForKey(SingleInstanceActivation.InstanceKey);
        if (!mainInstance.IsCurrent)
        {
            var activationArguments = currentInstance.GetActivatedEventArgs();
            // This happens before WinUI owns the UI thread.  Awaiting from an async
            // process entry point leaves the thread in a state that breaks TSF/IME
            // composition in WinUI text controls. Complete the redirect synchronously
            // before Application.Start establishes the WinUI dispatcher instead.
            mainInstance.RedirectActivationToAsync(activationArguments).AsTask().GetAwaiter().GetResult();
            return;
        }

        mainInstance.Activated += (_, activationArguments) => App.OnRedirectedActivation(activationArguments);
        Application.Start(callbackParameters =>
        {
            var synchronizationContext = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(synchronizationContext);
            _ = new App();
        });
    }
}
