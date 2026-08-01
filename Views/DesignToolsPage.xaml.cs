using Microsoft.UI.Xaml.Controls;
using UrbanPlanToolbox.Models.Tools;

namespace UrbanPlanToolbox.Views;

public sealed partial class DesignToolsPage : Page
{
    public DesignToolsPage()
    {
        InitializeComponent();
        CategoryBrowser.Configure(
            ToolPrimaryCategory.Design,
            ToolCategoryCatalog.Design,
            ToolSecondaryCategory.MasterPlanning);
    }
}
