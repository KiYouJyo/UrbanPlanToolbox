using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UrbanPlanToolbox.Models.Tools;
using UrbanPlanToolbox.Models.Projects;
using UrbanPlanToolbox.Services;

namespace UrbanPlanToolbox.Views;
public sealed partial class HomePage : Page
{
    private readonly ProjectStorageService _projects = ProjectStorageService.Default;
    private readonly ILocalizationService _localization = LocalizationService.Default;

    public HomePage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        var result = await _projects.ListAsync(false);
        ProjectsList.ItemsSource = result.Projects.Select(project => new ProjectCard(project, _localization)).ToArray();
        EmptyText.Visibility = result.Projects.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        StatusBar.IsOpen = result.Issues.Count > 0;
        StatusBar.Severity = InfoBarSeverity.Warning;
        StatusBar.Message = _localization.GetFormattedString("ProjectHome_LoadIssues", result.Issues.Count);
    }

    private async void OnNewProject(object sender, RoutedEventArgs e)
    {
        var name = new TextBox { Header = _localization.GetString("Project_Field_Name"), MaxLength = ProjectValidation.MaxNameLength };
        var type = new ComboBox { Header = _localization.GetString("Project_Field_Type"), DisplayMemberPath = "Name" };
        type.ItemsSource = ProjectTypeCodes.All.Select(code => new ProjectTypeOption(code, ProjectPresentation.GetTypeName(code, _localization))).ToArray();
        type.SelectedIndex = 0;
        var customType = new TextBox { Header = _localization.GetString("Project_Field_CustomType"), MaxLength = ProjectValidation.MaxTypeLength, Visibility = Visibility.Collapsed };
        type.SelectionChanged += (_, _) => customType.Visibility = (type.SelectedItem as ProjectTypeOption)?.Code == ProjectTypeCodes.Other ? Visibility.Visible : Visibility.Collapsed;
        var area = new TextBox { Header = _localization.GetString("Project_Field_AdministrativeArea"), MaxLength = ProjectValidation.MaxAdministrativeAreaLength };
        var latitude = new TextBox { Header = _localization.GetString("Project_Field_Latitude") };
        var longitude = new TextBox { Header = _localization.GetString("Project_Field_Longitude") };
        var description = new TextBox { Header = _localization.GetString("Project_Field_Description"), MaxLength = ProjectValidation.MaxDescriptionLength, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 90 };
        var error = new TextBlock { Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCriticalBrush"], TextWrapping = TextWrapping.Wrap };
        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(name); panel.Children.Add(type); panel.Children.Add(customType); panel.Children.Add(area);
        panel.Children.Add(new TextBlock { Text = _localization.GetString("Project_Coordinates_Wgs84.Text"), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        panel.Children.Add(latitude); panel.Children.Add(longitude); panel.Children.Add(description); panel.Children.Add(error);
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = _localization.GetString("Project_New_Title"), Content = new ScrollViewer { Content = panel, MaxHeight = 560 }, PrimaryButtonText = _localization.GetString("Action_Create"), CloseButtonText = _localization.GetString("Action_Cancel"), DefaultButton = ContentDialogButton.Primary };
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            args.Cancel = true;
            var deferral = args.GetDeferral();
            try
            {
                if (!TryParseCoordinate(latitude.Text, out var lat) || !TryParseCoordinate(longitude.Text, out var lon)) { error.Text = _localization.GetString("Project_Error_InvalidCoordinate"); return; }
                var selected = (ProjectTypeOption)type.SelectedItem;
                var result = await _projects.CreateAsync(name.Text, selected.Code, customType.Text, area.Text, lat, lon, description.Text);
                if (!result.Succeeded) { error.Text = LocalizeValidation(result.ValidationErrors); return; }
                args.Cancel = false;
                await RefreshAsync();
                Frame.Navigate(typeof(ProjectWorkspacePage), result.Project!.Id);
            }
            finally { deferral.Complete(); }
        };
        await dialog.ShowAsync();
    }

    private void OnOpenProject(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is ProjectRecord project) Frame.Navigate(typeof(ProjectWorkspacePage), project.Id);
    }

    private string LocalizeValidation(IReadOnlyList<string>? errors) =>
        errors is null ? _localization.GetString("Project_Error_SaveFailed") : string.Join(Environment.NewLine, errors.Select(error => _localization.GetString($"ProjectValidation_{error}")));

    private static bool TryParseCoordinate(string text, out decimal? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text)) return true;
        if (decimal.TryParse(text, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.CurrentCulture, out var parsed) ||
            decimal.TryParse(text, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out parsed)) { value = parsed; return true; }
        return false;
    }

    private sealed record ProjectTypeOption(string Code, string Name);
    private sealed record ProjectCard(ProjectRecord Project, string Name, string TypeAndArea, string Statistics, string Updated)
    {
        public ProjectCard(ProjectRecord project, ILocalizationService localization) : this(
            project,
            project.Name,
            string.IsNullOrWhiteSpace(project.AdministrativeArea) ? ProjectPresentation.GetTypeName(project, localization) : $"{ProjectPresentation.GetTypeName(project, localization)} · {project.AdministrativeArea}",
            localization.GetFormattedString("Project_Card_Statistics", project.Todos.Count(item => item.IsCompleted), project.Todos.Count, project.PlanningSnapshots.Count),
            localization.GetFormattedString("Project_Card_Updated", project.UpdatedAtUtc.ToLocalTime().ToString("g"))) { }
    }
}
