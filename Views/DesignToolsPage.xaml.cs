using Microsoft.UI.Xaml.Controls;
using UrbanPlanToolbox.Models.Tools;
using UrbanPlanToolbox.Services;
using UrbanPlanToolbox.Models;

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
        InspirationCards.Configure(InspirationCategory.Design);
    }
}
