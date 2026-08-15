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
using Microsoft.Windows.AppLifecycle;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace UrbanPlanToolbox;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private static readonly object ActivationGate = new();
    private Window? _window;
    private static bool _redirectedActivationPending;
    private static bool _mainWindowVisible;
    public static MainWindow? MainWindow { get; private set; }
    private static Views.InspirationRecorderWindow? _recorder;
    private static TrayService? _tray;
    public static async Task ShowInspirationRecorderAsync(bool moveToPrimaryWorkAreaTopRight = false)
    {
        if (_recorder is null)
        {
            _recorder = new Views.InspirationRecorderWindow();
            StartupTiming.Default.Mark("T5 Recorder window created");
        }
        await _recorder.RefreshAsync();
        StartupTiming.Default.Mark("T4 Inspiration and draft ready");
        _recorder.ShowRecorder(moveToPrimaryWorkAreaTopRight);
        StartupTiming.Default.Mark("T6 Recorder shown and activated");
        _tray?.SetRecorderVisible(true);
    }
    public static void HideInspirationRecorder()
    {
        _recorder?.HideRecorder();
        _tray?.SetRecorderVisible(false);
    }
    public static void ApplyBackgroundResidency(bool enabled)
    {
        if (enabled) { InitializeTray(); return; }
        HideInspirationRecorder();
        _tray?.Dispose();
        _tray = null;
    }
    public static async Task ShowInspirationAsync(Guid id)
    {
        _recorder ??= new Views.InspirationRecorderWindow();
        await _recorder.OpenInspirationAsync(id);
        _tray?.SetRecorderVisible(true);
    }
    public static void OpenInspirationManagement(Models.InspirationCategory category) => MainWindow?.DispatcherQueue.TryEnqueue(() =>
    {
        NotifyMainWindowShown();
        MainWindow.NavigateToInspiration(category);
    });
    internal static void NotifyMainWindowHidden()
    {
        _mainWindowVisible = false;
        _tray?.SetIconVisible(true);
        _tray?.SetRecorderVisible(_recorder?.IsVisible == true);
    }
    internal static void NotifyMainWindowShown()
    {
        _mainWindowVisible = true;
        _tray?.SetIconVisible(false);
    }
    private static void InitializeTray()
    {
        // The runtime switch can be toggled repeatedly.  Subscribe once only:
        // duplicate subscriptions would execute every tray command more than once.
        if (_tray is not null) return;
        _tray = new TrayService();
        _tray.Initialize(iconVisible: !_mainWindowVisible);
        StartupTiming.Default.Mark("T3 Tray ready");
        _tray.OpenRequested += (_, _) => MainWindow?.DispatcherQueue.TryEnqueue(() =>
        {
            NotifyMainWindowShown();
            MainWindow.RestoreAndActivate();
        });
        _tray.RecorderRequested += (_, _) => MainWindow?.DispatcherQueue.TryEnqueue(async () => { if (_recorder?.IsVisible == true) HideInspirationRecorder(); else await ShowInspirationRecorderAsync(); });
        _tray.SettingsRequested += (_, _) => MainWindow?.DispatcherQueue.TryEnqueue(() =>
        {
            NotifyMainWindowShown();
            MainWindow.NavigateToSettings();
        });
        _tray.ExitRequested += (_, _) => MainWindow?.DispatcherQueue.TryEnqueue(ExitApplication);
    }
    private static void ExitApplication() { _tray?.Dispose(); _recorder?.CloseForExit(); MainWindow?.CloseForExit(); Current.Exit(); }
    
    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        InitializeComponent();
        StartupTiming.Default.Mark("T0 App initialization start");
        UnhandledException += OnUnhandledException;
        AppLogger.Default.Info("App", "Constructed", "Application object initialized.");
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e) =>
        AppLogger.Default.Error("App", "UnhandledException", e.Exception, "Unhandled application exception.");

    /// <summary>Receives an activation redirected from a secondary process without creating another window.</summary>
    internal static void OnRedirectedActivation(AppActivationArguments activationArguments)
    {
        MainWindow? existingWindow;
        lock (ActivationGate)
        {
            existingWindow = MainWindow;
            if (existingWindow is null)
            {
                _redirectedActivationPending = true;
                return;
            }
        }

        existingWindow.DispatcherQueue.TryEnqueue(() =>
        {
            NotifyMainWindowShown();
            existingWindow.RestoreAndActivate();
        });
    }

    private static void ActivatePendingRedirectedWindow()
    {
        MainWindow? existingWindow;
        lock (ActivationGate)
        {
            if (!_redirectedActivationPending || MainWindow is null) return;
            _redirectedActivationPending = false;
            existingWindow = MainWindow;
        }

        existingWindow.DispatcherQueue.TryEnqueue(() =>
        {
            NotifyMainWindowShown();
            existingWindow.RestoreAndActivate();
        });
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        StartupTiming.Default.Mark($"T1 Launch activation received; background={Program.IsBackgroundStartup}");
        // This must precede SettingsService.Load and all other initialization:
        // a newly-created default settings file is not evidence of an upgrade.
        var firstRunExperience = FirstRunExperienceService.Default;
        firstRunExperience.PrepareForLaunch();
        var settings = new SettingsService().Load();
        StartupTiming.Default.Mark("T2 Minimum settings loaded");
        LocalizationService.Default.ApplyPersistedLanguage(settings);
        StartupTiming.Default.Mark("T7 Language applied");
        StartupTiming.Default.Mark("T2 MainWindow creation start");
        var systemBackground = new Windows.UI.ViewManagement.UISettings()
            .GetColorValue(Windows.UI.ViewManagement.UIColorType.Background);
        var systemUsesLightTheme = (0.2126 * systemBackground.R) + (0.7152 * systemBackground.G) + (0.0722 * systemBackground.B) >= 128;
        _window = MainWindow = new MainWindow(settings.Theme, systemUsesLightTheme);
        ActivatePendingRedirectedWindow();
        StartupTiming.Default.Mark("Startup.WindowCreated");
        StartupTiming.Default.Mark("T4 Root content ready");
        MainWindow.ApplyTheme(settings.Theme);
        StartupTiming.Default.Mark("Startup.ThemeResolved");
        StartupTiming.Default.Mark("T5 Theme ready");
        var startupWorkCompleted = false;
        MainWindow!.ShellReady += (_, _) =>
        {
            if (startupWorkCompleted) MainWindow?.ShowFirstRunGuideIfNeeded();
        };
        if (!Program.IsBackgroundStartup)
        {
            _window.Activate();
            NotifyMainWindowShown();
        }
        else
        {
            // A background StartupTask must never flash the full shell.  The recorder
            // remains an independently shown window; the shell is restored on demand.
            _mainWindowVisible = false;
            MainWindow.AppWindow.Hide();
            if (settings.BackgroundResidencyEnabled && settings.SilentStartupShowRecorder)
                _window.DispatcherQueue.TryEnqueue(async () => await ShowInspirationRecorderAsync(moveToPrimaryWorkAreaTopRight: true));
        }
        // The native tray belongs only to the opt-in residency lifecycle.
        if (settings.BackgroundResidencyEnabled) InitializeTray();
        StartupTiming.Default.Mark("Startup.WindowActivated");
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
