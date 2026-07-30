using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace UrbanPlanToolbox.Views;
public sealed partial class AboutPage : Page
{
    public AboutPage() => InitializeComponent();
    private void OnCheckUpdate(object sender, RoutedEventArgs e) => UpdateBar.IsOpen = true;
}
