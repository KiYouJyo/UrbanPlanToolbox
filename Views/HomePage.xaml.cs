using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UrbanPlanToolbox.Models.Tools;
using UrbanPlanToolbox.Services;

namespace UrbanPlanToolbox.Views;
public sealed partial class HomePage : Page
{
    public HomePage() => InitializeComponent();
    private void OnOpenCalculator(object sender, RoutedEventArgs e) => ToolNavigation.Navigate(Frame, ToolIds.PlanningIndicatorCalculator);
    private void OnOpenUnitScale(object sender, RoutedEventArgs e) => ToolNavigation.Navigate(Frame, ToolIds.UnitScaleConverter);
}
