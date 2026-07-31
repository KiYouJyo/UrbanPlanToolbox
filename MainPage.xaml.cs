using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
        ContentFrame.Navigate(typeof(Views.HomePage));
    }

    private void OnNavigationLoaded(object sender, RoutedEventArgs e)
    {
        if (Navigation.SettingsItem is NavigationViewItem settingsItem)
        {
            settingsItem.Content = "设置";
        }

        Navigation.SelectedItem = Navigation.MenuItems[0];
        if (ContentFrame.CurrentSourcePageType != typeof(Views.HomePage))
        {
            ContentFrame.Navigate(typeof(Views.HomePage));
        }
    }

    private void OnNavigationSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            NavigateTo(typeof(Views.SettingsPage));
        }
        else if (args.SelectedItem is NavigationViewItem item)
        {
            var route = item.Tag?.ToString();
            var page = route switch
            {
                "about" => typeof(Views.AboutPage),
                "home" => typeof(Views.HomePage),
                _ => null
            };

            if (page is not null)
            {
                NavigateTo(page);
            }
            else
            {
                ToolNavigation.Navigate(ContentFrame, route);
            }
        }
    }

    private void NavigateTo(Type page)
    {
        if (ContentFrame.CurrentSourcePageType != page) ContentFrame.Navigate(page);
    }
}
