using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

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
        Type page;
        if (args.IsSettingsSelected)
        {
            page = typeof(Views.SettingsPage);
        }
        else if (args.SelectedItem is NavigationViewItem item)
        {
            page = item.Tag?.ToString() switch
            {
                "calculator" => typeof(Views.PlanningCalculatorPage),
                "unit-scale" => typeof(Views.UnitScaleConverterPage),
                "about" => typeof(Views.AboutPage),
                _ => typeof(Views.HomePage)
            };
        }
        else
        {
            return;
        }

        if (ContentFrame.CurrentSourcePageType != page) ContentFrame.Navigate(page);
    }
}
