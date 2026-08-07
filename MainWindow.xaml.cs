using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
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
    private UIElement? _focusBeforeFirstRunGuide;
    private bool _firstRunGuideShowing;
    private bool _shellInitialized;
    private bool _shellLoaded;
    private bool _startupImageReady;
    private bool _shellReadyRaised;

    public event EventHandler? ShellReady;

    public MainWindow()
    {
        InitializeComponent();
        StartupTiming.Default.Mark("T3 InitializeComponent complete");

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "UrbanPlanToolbox.ico");
        AppWindow.SetIcon(iconPath);
        AppWindow.Resize(new SizeInt32(1100, 760));
        AppNotificationService.Default.NotificationRaised += OnNotificationRaised;
        _localization.LanguageChanged += OnLanguageChanged;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(NotificationBar, _localization.GetString("Interaction_NotificationName"));

        // The first page must exist before App activates this window; otherwise
        // native splash dismissal can reveal a title-bar-only black frame.
        FirstRunGuide.Closed += OnFirstRunGuideClosed;
    }

    private void OnRootLayoutLoaded(object sender, RoutedEventArgs e)
    {
        if (_shellInitialized) return;
        DispatcherQueue.TryEnqueue(InitializeShell);
    }

    private void InitializeShell()
    {
        if (_shellInitialized) return;
        _shellInitialized = true;
        RootFrame.Navigate(typeof(MainPage));
        if (RootFrame.Content is not FrameworkElement page)
            throw new InvalidOperationException("Main window first-frame content was not created.");
        page.Loaded += OnShellLoaded;
    }

    private void OnShellLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement page) page.Loaded -= OnShellLoaded;
        _shellLoaded = true;
        StartupTiming.Default.Mark("T9 Main page Loaded / first usable UI");
        TryCompleteStartupVisual();
    }

    private void OnStartupLogoImageOpened(object sender, RoutedEventArgs e)
    {
        _startupImageReady = true;
        TryCompleteStartupVisual();
    }

    private void OnStartupLogoImageFailed(object sender, ExceptionRoutedEventArgs e)
    {
        // Do not leave the application on the startup layer if the image fails;
        // the shell is still a valid fallback.
        _startupImageReady = true;
        TryCompleteStartupVisual();
    }

    private void TryCompleteStartupVisual()
    {
        if (!_shellLoaded || !_startupImageReady || _shellReadyRaised) return;
        _shellReadyRaised = true;
        StartupOverlay.Visibility = Visibility.Collapsed;
        ShellReady?.Invoke(this, EventArgs.Empty);
    }

    public void Navigate(Type pageType) => RootFrame.Navigate(pageType);

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
