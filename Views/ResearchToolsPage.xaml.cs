using Microsoft.UI.Xaml.Controls;
using UrbanPlanToolbox.Models.Tools;

namespace UrbanPlanToolbox.Views;

public sealed partial class ResearchToolsPage : Page
{
    public ResearchToolsPage()
    {
        InitializeComponent();
        CategoryBrowser.Configure(
            ToolPrimaryCategory.Research,
            ToolCategoryCatalog.Research,
            ToolSecondaryCategory.ResearchPreparation);
    }
}
