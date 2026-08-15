using Microsoft.UI.Xaml;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.AppLifecycle;
using UrbanPlanToolbox.Services;
using Windows.ApplicationModel;
using WinRT;

namespace UrbanPlanToolbox;

/// <summary>Owns process startup so instance arbitration occurs before WinUI is initialized.</summary>
public static class Program
{
    private static PackageCatalog? _packageCatalog;
    private static string? _currentPackageFullName;

    public static bool IsBackgroundStartup { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        ComWrappersSupport.InitializeComWrappers();
        StartPackageUninstallExitWatcher();

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

    /// <summary>
    /// A resident desktop process can otherwise keep its MSIX package in use and block removal.
    /// Observe the current package only and terminate the process as soon as its uninstall begins,
    /// so users do not have to find and end the hidden tray process manually.
    /// </summary>
    private static void StartPackageUninstallExitWatcher()
    {
        try
        {
            _currentPackageFullName = Package.Current.Id.FullName;
            _packageCatalog = PackageCatalog.OpenForCurrentPackage();
            _packageCatalog.PackageUninstalling += OnPackageUninstalling;
        }
        catch
        {
            // Unpackaged/design-time launches do not have a current package catalog.
            // They should continue to start normally; this watcher is relevant only to MSIX installs.
            _packageCatalog = null;
            _currentPackageFullName = null;
        }
    }

    private static void OnPackageUninstalling(PackageCatalog sender, PackageUninstallingEventArgs args)
    {
        if (args.IsComplete || string.IsNullOrWhiteSpace(_currentPackageFullName)) return;

        try
        {
            if (!string.Equals(args.Package.Id.FullName, _currentPackageFullName, StringComparison.OrdinalIgnoreCase)) return;
        }
        catch
        {
            return;
        }

        // Do not marshal back through WinUI here. The deployment operation is waiting for the
        // package to stop being in use, so release the process immediately and deterministically.
        Environment.Exit(0);
    }
}