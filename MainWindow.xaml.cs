using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
    private readonly FirstRunExperienceService _firstRunExperience = new();

    public MainWindow()
    {
        InitializeComponent();

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
        RootFrame.Navigate(typeof(MainPage));
        if (RootFrame.Content is null) throw new InvalidOperationException("Main window first-frame content was not created.");
        FirstRunGuide.Closed += OnFirstRunGuideClosed;
    }

    public void Navigate(Type pageType) => RootFrame.Navigate(pageType);

    public void ShowFirstRunGuideFromSettings() => ShowFirstRunGuide(manual: true);

    public void ShowFirstRunGuideIfNeeded()
    {
        if (_firstRunExperience.ShouldShowAutomatically()) ShowFirstRunGuide(manual: false);
    }

    private void ShowFirstRunGuide(bool manual)
    {
        if (FirstRunGuide.Visibility == Visibility.Visible) return;
        if (RootFrame.Content is MainPage currentPage) _navigationState.Save(currentPage.CaptureState());
        FirstRunGuide.Show(manual);
    }

    private void OnFirstRunGuideClosed(object? sender, EventArgs e) { }

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
