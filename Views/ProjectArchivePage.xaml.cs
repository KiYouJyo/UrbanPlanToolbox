using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using UrbanPlanToolbox.Models.Projects;
using UrbanPlanToolbox.Services;

namespace UrbanPlanToolbox.Views;

public sealed partial class ProjectArchivePage : Page
{
    public ProjectArchivePage()
    {
        InitializeComponent();
        TitleText.Text = LocalizationService.Default.GetString("Navigation_ProjectArchive");
        Loaded += async (_, _) => await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        var localization = LocalizationService.Default;
        var result = await ProjectStorageService.Default.ListAsync(true);
        ProjectsList.ItemsSource = result.Projects.Select(project => new ArchiveCard(project, localization)).ToArray();
        EmptyText.Visibility = result.Projects.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        StatusBar.IsOpen = result.Issues.Count > 0;
        StatusBar.Message = localization.GetFormattedString("ProjectHome_LoadIssues", result.Issues.Count);
    }

    private void OnOpenProject(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is ProjectRecord project) Frame.Navigate(typeof(ProjectWorkspacePage), project.Id);
    }

    private sealed record ArchiveCard(ProjectRecord Project, string Name, string TypeAndArea, string Statistics, string Archived)
    {
        public ArchiveCard(ProjectRecord project, ILocalizationService localization) : this(
            project, project.Name,
            string.IsNullOrWhiteSpace(project.AdministrativeArea) ? ProjectPresentation.GetTypeName(project, localization) : $"{ProjectPresentation.GetTypeName(project, localization)} · {project.AdministrativeArea}",
            localization.GetFormattedString("Project_Card_Milestones", project.Milestones.Count),
            localization.GetFormattedString("ProjectArchive_ArchivedAt", project.ArchivedAtUtc?.ToLocalTime().ToString("g") ?? "—")) { }
    }
}
