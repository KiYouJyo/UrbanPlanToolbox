using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace UrbanPlanToolbox.Views;
public sealed partial class HomePage : Page
{
    public HomePage() => InitializeComponent();
    private void OnOpenCalculator(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(PlanningCalculatorPage));
}
