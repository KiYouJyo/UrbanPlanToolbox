using Microsoft.UI.Xaml.Controls;
using UrbanPlanToolbox.Services;

namespace UrbanPlanToolbox.Views;

public sealed partial class ProjectArchivePage : Page
{
    public ProjectArchivePage()
    {
        InitializeComponent();
        TitleText.Text = LocalizationService.Default.GetString("Navigation_ProjectArchive");
    }
}
