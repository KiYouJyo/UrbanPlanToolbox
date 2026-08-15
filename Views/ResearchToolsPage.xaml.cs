using Microsoft.UI.Xaml.Controls;
using UrbanPlanToolbox.Models.Tools;
using UrbanPlanToolbox.Services;

namespace UrbanPlanToolbox.Views;

public sealed partial class ResearchToolsPage : Page
{
    public ResearchToolsPage()
    {
        InitializeComponent();
        TitleText.Text = LocalizationService.Default.GetString("Navigation_ResearchTools");
        CategoryBrowser.Configure(
            ToolPrimaryCategory.Research,
            ToolCategoryCatalog.Research,
            ToolSecondaryCategory.ResearchPreparation);
    }
}
