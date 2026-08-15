using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics;
using UrbanPlanToolbox.Models.Interaction;
using UrbanPlanToolbox.Services;
using UrbanPlanToolbox.Views;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace UrbanPlanToolbox;

/// <summary>
/// The application window. This hosts a Frame that displays pages. Add your
/// UI and logic to MainPage.xaml / MainPage.xaml.cs instead of here so you
/// can use Page features such as navigation events and the Loaded lifecycle.
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly INavigationStateService _navigationState = new NavigationStateService();
    private readonly LocalizationService _localization = LocalizationService.Default;
    private readonly FirstRunExperienceService _firstRunExperience = FirstRunExperienceService.Default;
    private readonly WindowPlacementService _windowPlacement = new();
    private readonly Windows.UI.ViewManagement.UISettings _uiSettings = new();
    private string? _themePreference;
    private UIElement? _focusBeforeFirstRunGuide;
    private bool _firstRunGuideShowing;
    private bool _shellInitialized;
    private bool _shellLoaded;
    private Image? _selectedStartupLogo;
    private bool _startupImageReady;
    private bool _startupSplashRenderRequested;
    private bool _startupSplashShown;
    private bool _minimumSplashDurationSatisfied;
    private bool _shellReadyRaised;
    private bool _startupWatchdogStarted;
    private readonly Stopwatch _startupSplashVisibleClock = new();
    private SizeInt32 _lastNormalWindowSize;
    private bool _wasWindowMaximized;
    private bool _allowClose;

    public event EventHandler? ShellReady;

    public MainWindow(string? startupThemePreference, bool systemUsesLightTheme)
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        _themePreference = startupThemePreference;
        ApplyWindowChromeTheme(WindowIconTheme.Resolve(_themePreference, systemUsesLightTheme));
        _uiSettings.ColorValuesChanged += OnSystemColorValuesChanged;
        RestoreWindowPlacement();
        AppWindow.Changed += OnAppWindowChanged;
        AppWindow.Closing += OnAppWindowClosing;
        Closed += OnWindowClosed;

        StartupTiming.Default.Mark("T3 InitializeComponent complete");
        StartupTiming.Default.Mark("Startup.MicaReady");

        var splashTheme = StartupSplashPresentation.ResolveTheme(startupThemePreference, systemUsesLightTheme);
        // Existing XAML names are retained to avoid changing the accepted asset surface;
        // they represent the target app theme, not the glyph color.
        _selectedStartupLogo = splashTheme == StartupSplashTheme.Light ? StartupLightLogo : StartupDarkLogo;
        StartupDarkLogo.Visibility = splashTheme == StartupSplashTheme.Dark ? Visibility.Visible : Visibility.Collapsed;
        StartupLightLogo.Visibility = splashTheme == StartupSplashTheme.Light ? Visibility.Visible : Visibility.Collapsed;
        StartupTiming.Default.Mark("Startup.OverlayCreated");

        AppNotificationService.Default.NotificationRaised += OnNotificationRaised;
        _localization.LanguageChanged += OnLanguageChanged;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(NotificationBar, _localization.GetString("Interaction_NotificationName"));

        // The first page must exist before App activates this window; otherwise
        // native splash dismissal can reveal a title-bar-only black frame.
        FirstRunGuide.Closed += OnFirstRunGuideClosed;
    }
    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_allowClose || !new SettingsService().Load().BackgroundResidencyEnabled) return;
        args.Cancel = true; AppWindow.Hide(); App.NotifyMainWindowHidden();
    }
    public void CloseForExit() { _allowClose = true; Close(); }

    private void RestoreWindowPlacement()
    {
        var workArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        var placement = _windowPlacement.Load(new SizeInt32(workArea.Width, workArea.Height));
        _lastNormalWindowSize = new SizeInt32(placement.Width, placement.Height);
        _wasWindowMaximized = placement.WasMaximized;
        AppWindow.Resize(_lastNormalWindowSize);
        if (_wasWindowMaximized && AppWindow.Presenter is OverlappedPresenter presenter)
            presenter.Maximize();
    }

    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (AppWindow.Presenter is not OverlappedPresenter presenter) return;
        switch (presenter.State)
        {
            case OverlappedPresenterState.Maximized:
                _wasWindowMaximized = true;
                break;
            case OverlappedPresenterState.Restored:
                _wasWindowMaximized = false;
                if (args.DidSizeChange) _lastNormalWindowSize = AppWindow.Size;
                break;
            // A minimized window is never restored as minimized; retain the last usable state.
            case OverlappedPresenterState.Minimized:
                break;
        }
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        AppWindow.Changed -= OnAppWindowChanged;
        _uiSettings.ColorValuesChanged -= OnSystemColorValuesChanged;
        Closed -= OnWindowClosed;
        try { _windowPlacement.Save(_lastNormalWindowSize, _wasWindowMaximized); }
        catch (Exception exception) { AppLogger.Default.Error("WindowPlacement", "SaveFailed", exception, "Could not save window placement."); }
    }

    /// <summary>Restores and foregrounds this already-created main window for redirected activation.</summary>
    public void RestoreAndActivate()
    {
        AppWindow.Show();
        if (AppWindow.Presenter is OverlappedPresenter presenter && presenter.State == OverlappedPresenterState.Minimized)
            presenter.Restore();

        Activate();
        SetForegroundWindow(WinRT.Interop.WindowNative.GetWindowHandle(this));
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint hWnd);

    /// <summary>Applies the app theme and synchronizes only runtime window chrome.</summary>
    public void ApplyTheme(string? preference)
    {
        _themePreference = preference;
        ThemePreference.Apply(Content as FrameworkElement, preference);
        ApplyWindowChromeTheme(WindowIconTheme.Resolve(preference, SystemUsesLightTheme()));
    }

    private void OnSystemColorValuesChanged(Windows.UI.ViewManagement.UISettings sender, object args)
    {
        if (SettingsService.NormalizeTheme(_themePreference) != AppTheme.System) return;
        DispatcherQueue.TryEnqueue(() => ApplyWindowChromeTheme(WindowIconTheme.Resolve(_themePreference, SystemUsesLightTheme())));
    }

    private bool SystemUsesLightTheme()
    {
        var background = _uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Background);
        return (0.2126 * background.R) + (0.7152 * background.G) + (0.0722 * background.B) >= 128;
    }

    private void ApplyWindowChromeTheme(AppTheme resolvedTheme)
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, WindowIconTheme.GetIconRelativePath(resolvedTheme));
        AppWindow.SetIcon(iconPath);
        AppTitleBar.IconSource = new ImageIconSource { ImageSource = new BitmapImage(new Uri(WindowIconTheme.GetLogoUri(resolvedTheme))) };

        if (!AppWindowTitleBar.IsCustomizationSupported()) return;
        AppWindow.TitleBar.PreferredTheme = resolvedTheme == AppTheme.Dark ? TitleBarTheme.Dark : TitleBarTheme.Light;
    }

    private void OnRootLayoutLoaded(object sender, RoutedEventArgs e)
    {
        if (_shellInitialized) return;
        StartStartupSafetyNets();
        StartupTiming.Default.Mark("Startup.InitializationStarted");
        DispatcherQueue.TryEnqueue(InitializeShell);
    }

    private void StartStartupSafetyNets()
    {
        if (_startupWatchdogStarted) return;
        _startupWatchdogStarted = true;
        _ = EnsureLogoGateAsync();
        _ = WatchStartupAsync();
    }

    private async Task EnsureLogoGateAsync()
    {
        await Task.Delay(TimeSpan.FromSeconds(1));
        if (_startupImageReady || _shellReadyRaised) return;
        // ImageOpened can be skipped when a packaged source is already decoded.
        // The visible XAML image is still a safe startup surface; never await it forever.
        _startupImageReady = true;
        StartupTiming.Default.Mark("Startup.LogoImageFallback");
        StartMinimumSplashDurationAfterFirstRender();
        TryCompleteStartupVisual();
    }

    private async Task WatchStartupAsync()
    {
        await Task.Delay(TimeSpan.FromSeconds(5));
        if (_shellReadyRaised) return;
        StartupTiming.Default.Mark("StartupWatchdogTriggered");
        PresentMainContent("Startup.WatchdogFailOpen");
    }

    private void InitializeShell()
    {
        if (_shellInitialized) return;
        _shellInitialized = true;
        StartupTiming.Default.Mark("Startup.MainContentCreated");
        RootFrame.Navigate(typeof(MainPage));
        if (RootFrame.Content is not FrameworkElement page)
            throw new InvalidOperationException("Main window first-frame content was not created.");
        page.Loaded += OnShellLoaded;
    }

    private void OnShellLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement page) page.Loaded -= OnShellLoaded;
        _shellLoaded = true;
        StartupTiming.Default.Mark("Startup.MainContentLoaded");
        StartupTiming.Default.Mark("T9 Main page Loaded / first usable UI");
        StartupTiming.Default.Mark("StartupInitializationCompleted");
        StartupTiming.Default.Mark("Startup.MainContentReady");
        TryCompleteStartupVisual();
    }

    private void OnStartupLogoImageOpened(object sender, RoutedEventArgs e)
    {
        if (!ReferenceEquals(sender, _selectedStartupLogo)) return;
        _startupImageReady = true;
        StartupTiming.Default.Mark("Startup.LogoImageOpened");
        StartMinimumSplashDurationAfterFirstRender();
        TryCompleteStartupVisual();
    }

    private void OnStartupLogoImageFailed(object sender, ExceptionRoutedEventArgs e)
    {
        if (!ReferenceEquals(sender, _selectedStartupLogo)) return;
        // Do not leave the application on the startup layer if the image fails;
        // the shell is still a valid fallback.
        _startupImageReady = true;
        StartupTiming.Default.Mark("Startup.LogoImageFailed");
        StartMinimumSplashDurationAfterFirstRender();
        TryCompleteStartupVisual();
    }

    private void StartMinimumSplashDurationAfterFirstRender()
    {
        if (_startupSplashRenderRequested) return;
        _startupSplashRenderRequested = true;
        CompositionTarget.Rendering += OnStartupSplashRendered;
    }

    private void OnStartupSplashRendered(object? sender, object e)
    {
        CompositionTarget.Rendering -= OnStartupSplashRendered;
        StartupTiming.Default.Mark("Startup.FirstOverlayFrameRendered");
        StartMinimumSplashDuration();
    }

    private void StartMinimumSplashDuration()
    {
        if (_startupSplashShown) return;
        _startupSplashShown = true;
        _startupSplashVisibleClock.Start();
        StartupTiming.Default.Mark("SplashShown");
        StartupTiming.Default.Mark("Startup.MinimumTimerStarted");
        _ = CompleteMinimumSplashDurationAsync();
    }

    private async Task CompleteMinimumSplashDurationAsync()
    {
        var remaining = StartupSplashTiming.RemainingMinimumVisibleDuration(_startupSplashVisibleClock);
        if (remaining > TimeSpan.Zero) await Task.Delay(remaining);
        _minimumSplashDurationSatisfied = true;
        StartupTiming.Default.Mark("MinimumSplashDurationSatisfied");
        StartupTiming.Default.Mark("Startup.MinimumTimerCompleted");
        TryCompleteStartupVisual();
    }

    private void TryCompleteStartupVisual()
    {
        if (!_shellLoaded || !_startupImageReady || !_minimumSplashDurationSatisfied || _shellReadyRaised) return;
        StartupTiming.Default.Mark("Startup.DismissConditionsWaiting");
        PresentMainContent("Startup.DismissConditionsSatisfied");
    }

    private void PresentMainContent(string reason)
    {
        if (_shellReadyRaised) return;
        _shellReadyRaised = true;
        StartupTiming.Default.Mark(reason);
        MainContent.Opacity = 1;
        _ = FadeOutStartupOverlayAsync();
    }

    private async Task FadeOutStartupOverlayAsync()
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            StartupTiming.Default.Mark("Startup.FadeStarted");
            var storyboard = new Storyboard();
            var fade = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = new Duration(StartupSplashTiming.FadeOutDuration),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(fade, StartupOverlay);
            Storyboard.SetTargetProperty(fade, nameof(UIElement.Opacity));
            storyboard.Children.Add(fade);
            storyboard.Completed += (_, _) => completion.TrySetResult(true);
            storyboard.Begin();

            if (await Task.WhenAny(completion.Task, Task.Delay(StartupSplashTiming.FadeOutFallbackDuration)) != completion.Task)
                StartupTiming.Default.Mark("Startup.FadeFallback");
        }
        catch (Exception exception)
        {
            AppLogger.Default.Error("Startup", "FadeFailed", exception, "Startup overlay fade failed; presenting the shell directly.");
            StartupTiming.Default.Mark("Startup.FadeFailed");
        }
        finally
        {
            StartupOverlay.Visibility = Visibility.Collapsed;
            StartupOverlay.Opacity = 1;
            MainContent.IsHitTestVisible = true;
            StartupTiming.Default.Mark("Startup.FadeCompleted");
            StartupTiming.Default.Mark("Startup.MainContentShown");
            StartupTiming.Default.Mark("Startup.OverlayRemoved");
            StartupTiming.Default.Mark("Startup.Completed");
            ShellReady?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Navigate(Type pageType) => RootFrame.Navigate(pageType);
    public void NavigateToSettings()
    {
        RestoreAndActivate();
        if (RootFrame.Content is MainPage mainPage) mainPage.NavigateToSettings();
        else { RootFrame.Navigate(typeof(MainPage)); DispatcherQueue.TryEnqueue(() => (RootFrame.Content as MainPage)?.NavigateToSettings()); }
    }
    public void NavigateToInspiration(Models.InspirationCategory category)
    {
        RestoreAndActivate();
        if (RootFrame.Content is MainPage mainPage) mainPage.NavigateToInspiration(category);
        else { RootFrame.Navigate(typeof(MainPage)); DispatcherQueue.TryEnqueue(() => (RootFrame.Content as MainPage)?.NavigateToInspiration(category)); }
    }

    /// <summary>Single window-level coordinator used by Settings and startup.</summary>
    public void ShowFirstRunGuideFromSettings() => ShowFirstRunGuide(FirstRunGuideLaunchMode.Manual);

    public void ShowFirstRunGuideIfNeeded()
    {
        if (_firstRunExperience.ShouldShowAutomatically())
            ShowFirstRunGuide(FirstRunGuideLaunchMode.Automatic);
    }

    private void ShowFirstRunGuide(FirstRunGuideLaunchMode mode)
    {
        if (_firstRunGuideShowing || FirstRunGuide.Visibility == Visibility.Visible) return;
        _firstRunGuideShowing = true;
        if (RootFrame.XamlRoot is not null)
            _focusBeforeFirstRunGuide = FocusManager.GetFocusedElement(RootFrame.XamlRoot) as UIElement;
        if (RootFrame.Content is MainPage currentPage) _navigationState.Save(currentPage.CaptureState());
        FirstRunGuide.Show(mode);
    }

    private void OnFirstRunGuideClosed(object? sender, EventArgs e)
    {
        _firstRunGuideShowing = false;
        var focusTarget = _focusBeforeFirstRunGuide;
        _focusBeforeFirstRunGuide = null;
        if (focusTarget is not null)
            DispatcherQueue.TryEnqueue(() => focusTarget.Focus(FocusState.Programmatic));
    }

    private void OnLanguageChanged(object? sender, LanguageChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(ReloadLocalizedShell);
    }

    private void ReloadLocalizedShell()
    {
        if (RootFrame.Content is MainPage currentPage)
        {
            _navigationState.Save(currentPage.CaptureState());
        }

        RootFrame.Content = null;
        RootFrame.Navigate(typeof(MainPage), _navigationState.Restore());
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(
            NotificationBar,
            _localization.GetString("Interaction_NotificationName"));
    }

    private void OnNotificationRaised(object? sender, AppNotification notification)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            NotificationBar.Title = notification.Title;
            NotificationBar.Message = notification.Message;
            NotificationBar.Severity = notification.Kind switch
            {
                AppNotificationKind.Success => InfoBarSeverity.Success,
                AppNotificationKind.Warning => InfoBarSeverity.Warning,
                AppNotificationKind.Error => InfoBarSeverity.Error,
                _ => InfoBarSeverity.Informational
            };
            NotificationBar.IsClosable = true;
            NotificationBar.IsOpen = true;
        });
    }
}
