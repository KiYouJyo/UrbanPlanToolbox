using Microsoft.UI.Xaml.Controls;
using UrbanPlanToolbox.Models.Tools;
using UrbanPlanToolbox.Services;

namespace UrbanPlanToolbox.Views;

public sealed partial class DesignToolsPage : Page
{
    public DesignToolsPage()
    {
        InitializeComponent();
        TitleText.Text = LocalizationService.Default.GetString("Navigation_DesignTools");
        CategoryBrowser.Configure(
            ToolPrimaryCategory.Design,
            ToolCategoryCatalog.Design,
            ToolSecondaryCategory.MasterPlanning);
    }
}
