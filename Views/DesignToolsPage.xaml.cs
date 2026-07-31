using Microsoft.UI.Xaml.Controls;
using UrbanPlanToolbox.Models.Tools;
using UrbanPlanToolbox.Services;

namespace UrbanPlanToolbox.Views;

public sealed partial class DesignToolsPage : Page
{
    public DesignToolsPage()
    {
        InitializeComponent();
        ToolsList.ItemsSource = ToolRegistry.Default.GetAvailableByPrimaryCategory(ToolPrimaryCategory.Design);
    }

    private void OnToolClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ToolDefinition tool)
        {
            ToolNavigation.Navigate(Frame, tool.Id);
        }
    }
}
