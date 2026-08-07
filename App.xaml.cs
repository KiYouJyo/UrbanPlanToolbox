using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using UrbanPlanToolbox.Services;
using Microsoft.Windows.Globalization;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace UrbanPlanToolbox;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private Window? _window;
    public static MainWindow? MainWindow { get; private set; }
    
    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        InitializeComponent();
        StartupTiming.Default.Mark("T0 App constructor");
        UnhandledException += OnUnhandledException;
        AppLogger.Default.Info("App", "Constructed", "Application object initialized.");
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e) =>
        AppLogger.Default.Error("App", "UnhandledException", e.Exception, "Unhandled application exception.");

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        StartupTiming.Default.Mark("T1 OnLaunched entered");
        // This must precede SettingsService.Load and all other initialization:
        // a newly-created default settings file is not evidence of an upgrade.
        var firstRunExperience = FirstRunExperienceService.Default;
        firstRunExperience.PrepareForLaunch();
        var settings = new SettingsService().Load();
        StartupTiming.Default.Mark("T6 Language and settings ready");
        LocalizationService.Default.ApplyPersistedLanguage(settings);
        StartupTiming.Default.Mark("T7 Language applied");
        StartupTiming.Default.Mark("T2 MainWindow creation start");
        _window = MainWindow = new MainWindow();
        StartupTiming.Default.Mark("T4 Root content ready");
        ThemePreference.Apply((FrameworkElement)_window.Content, settings.Theme);
        StartupTiming.Default.Mark("T5 Theme ready");
        var startupWorkCompleted = false;
        MainWindow!.ShellReady += (_, _) =>
        {
            if (startupWorkCompleted) MainWindow?.ShowFirstRunGuideIfNeeded();
        };
        _window.Activate();
        StartupTiming.Default.Mark("T8 Activate called");
        // Defer disk and notification work until a complete first frame is available.
        _window.DispatcherQueue.TryEnqueue(async () =>
        {
            var pipeline = new StartupPipeline();
            await pipeline.RunAfterFirstFrameAsync(
                async () =>
                {
                    try { await MilestoneReminderService.Default.RefreshAsync(); }
                    catch (Exception exception) { AppLogger.Default.Error("Startup", "NotificationRefreshFailed", exception, "Notification reconciliation failed."); }
                },
                () => Task.Run(() =>
                {
                    try { AppDataPathProvider.Default.EnsureInfrastructureDirectories(); AppLogger.Default.Info("App", "Launched", "Main window activated."); AppLogger.Default.RunRetention(); }
                    catch (Exception exception) { AppLogger.Default.Error("App", "StartupInfrastructureFailed", exception, "Local startup infrastructure failed."); }
                }),
                exception => AppLogger.Default.Error("Startup", "BackgroundTaskFailed", exception, "Background startup task failed."));
            StartupTiming.Default.Mark("T10 Background initialization complete");
            foreach (var point in StartupTiming.Default.Points)
                AppLogger.Default.Info("Startup", "Timing", $"{point.Name}; elapsedMs={point.ElapsedMilliseconds}; threadId={point.ThreadId}");
            startupWorkCompleted = true;
            MainWindow?.ShowFirstRunGuideIfNeeded();
        });
    }

}
