using Microsoft.UI.Xaml;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.AppLifecycle;
using UrbanPlanToolbox.Services;
using WinRT;

namespace UrbanPlanToolbox;

/// <summary>Owns process startup so instance arbitration occurs before WinUI is initialized.</summary>
public static class Program
{
    [STAThread]
    public static async Task Main(string[] args)
    {
        ComWrappersSupport.InitializeComWrappers();

        var currentInstance = AppInstance.GetCurrent();
        var mainInstance = AppInstance.FindOrRegisterForKey(SingleInstanceActivation.InstanceKey);
        if (!mainInstance.IsCurrent)
        {
            var activationArguments = currentInstance.GetActivatedEventArgs();
            await mainInstance.RedirectActivationToAsync(activationArguments);
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
