using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using UrbanPlanToolbox.Models.Navigation;
using UrbanPlanToolbox.Services;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace UrbanPlanToolbox;

/// <summary>
/// The main content page displayed inside the application window.
/// Add your UI logic, event handlers, and data binding here.
/// </summary>
public sealed partial class MainPage : Page
{
    public MainPage()
    {
        InitializeComponent();
        ApplyLocalizedNavigation();
        NavigateTo(typeof(Views.HomePage));
    }

    private void OnNavigationLoaded(object sender, RoutedEventArgs e)
    {
        if (Navigation.SettingsItem is NavigationViewItem settingsItem)
        {
            var settingsLabel = LocalizationService.Default.GetString("Navigation_Settings");
            settingsItem.Content = settingsLabel;
            AutomationProperties.SetName(settingsItem, settingsLabel);
            ToolTipService.SetToolTip(settingsItem, settingsLabel);
        }

        Navigation.SelectedItem = Navigation.MenuItems[0];
        NavigateTo(typeof(Views.HomePage));
    }

    private void ApplyLocalizedNavigation()
    {
        ApplyNavigationItem(WelcomeItem, PrimaryNavigationIds.Welcome);
        ApplyNavigationItem(SearchItem, PrimaryNavigationIds.CommonTools);
        ApplyNavigationItem(DesignItem, PrimaryNavigationIds.DesignTools);
        ApplyNavigationItem(ResearchItem, PrimaryNavigationIds.ResearchTools);
        ApplyNavigationItem(ArchiveItem, PrimaryNavigationIds.ProjectArchive);
        ApplyNavigationItem(AboutItem, PrimaryNavigationIds.About);
    }

    private static void ApplyNavigationItem(NavigationViewItem item, string routeId)
    {
        if (PrimaryNavigation.Default.TryGet(routeId, out var route) && route is not null)
        {
            var label = LocalizationService.Default.GetString(route.NameResourceKey);
            item.Content = label;
            AutomationProperties.SetName(item, label);
            ToolTipService.SetToolTip(item, label);
        }
    }

    private void OnNavigationItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.IsSettingsInvoked)
        {
            NavigateTo(typeof(Views.SettingsPage));
        }
        else if (args.InvokedItemContainer is NavigationViewItem item)
        {
            if (PrimaryNavigation.Default.TryGet(item.Tag?.ToString(), out var route) && route is not null)
            {
                NavigateTo(route.PageType);
            }
        }
    }

    private void NavigateTo(Type page)
    {
        if (ContentFrame.CurrentSourcePageType != page) ContentFrame.Navigate(page);
    }
}
