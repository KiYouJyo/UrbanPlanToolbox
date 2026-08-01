using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using UrbanPlanToolbox.Models.Projects;
using UrbanPlanToolbox.Services;

namespace UrbanPlanToolbox.Views;

public sealed partial class HomePage : Page
{
    public const string DesignCategoryId = "design-projects";
    public const string ResearchCategoryId = "research-projects";
    private static string _sessionKind = ProjectKindCodes.Design;
    private readonly ProjectStorageService _projects = ProjectStorageService.Default;
    private readonly ILocalizationService _localization = LocalizationService.Default;
    private IReadOnlyList<ProjectRecord> _loadedProjects = [];
    private bool _configuring;

    public HomePage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        var result = await _projects.ListAsync(false);
        _loadedProjects = result.Projects;
        ConfigureKindList();
        RenderCurrentKind();
        StatusBar.IsOpen = result.Issues.Count > 0;
        StatusBar.Severity = InfoBarSeverity.Warning;
        StatusBar.Message = _localization.GetFormattedString("ProjectHome_LoadIssues", result.Issues.Count);
    }

    private void ConfigureKindList()
    {
        _configuring = true;
        var options = new[]
        {
            new ProjectKindOption(DesignCategoryId, ProjectKindCodes.Design, _localization.GetFormattedString("ProjectCategory_DesignWithCount", _loadedProjects.Count(item => item.Kind == ProjectKindCodes.Design))),
            new ProjectKindOption(ResearchCategoryId, ProjectKindCodes.Research, _localization.GetFormattedString("ProjectCategory_ResearchWithCount", _loadedProjects.Count(item => item.Kind == ProjectKindCodes.Research)))
        };
        KindList.ItemsSource = options;
        KindList.SelectedItem = options.First(item => item.Kind == _sessionKind);
        _configuring = false;
    }

    private void OnKindChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_configuring || KindList.SelectedItem is not ProjectKindOption option) return;
        _sessionKind = option.Kind;
        RenderCurrentKind();
    }

    private void RenderCurrentKind()
    {
        var visible = _loadedProjects.Where(item => item.Kind == _sessionKind).OrderByDescending(item => item.UpdatedAtUtc).ToArray();
        ProjectsList.ItemsSource = visible.Select(project => new ProjectCard(project, _localization)).ToArray();
        ProjectsList.Visibility = visible.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        EmptyPanel.Visibility = visible.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyText.Text = _localization.GetString(_sessionKind == ProjectKindCodes.Research ? "ProjectHome_ResearchEmpty" : "ProjectHome_DesignEmpty");
        ConfigureButton(EmptyCreateButton, _sessionKind == ProjectKindCodes.Research ? "Project_Action_NewResearch" : "Project_Action_NewDesign");
    }

    private async void OnNewProject(object sender, RoutedEventArgs e)
    {
        var kind = await ChooseKindAsync();
        if (kind is not null) await ShowCreateDialogAsync(kind);
    }

    private async void OnCreateCurrentKind(object sender, RoutedEventArgs e) => await ShowCreateDialogAsync(_sessionKind);

    private async Task<string?> ChooseKindAsync()
    {
        var selection = new ListView { SelectionMode = ListViewSelectionMode.Single, IsItemClickEnabled = true, MaxHeight = 420 };
        selection.ItemsSource = new[]
        {
            new KindChoice(ProjectKindCodes.Design, _localization.GetString("ProjectKind_Design"), _localization.GetString("ProjectKind_DesignDescription")),
            new KindChoice(ProjectKindCodes.Research, _localization.GetString("ProjectKind_Research"), _localization.GetString("ProjectKind_ResearchDescription"))
        };
        selection.ItemTemplate = (DataTemplate)XamlReader.Load("<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><Border Padding='16' Margin='0,0,0,8' BorderThickness='1' BorderBrush='{ThemeResource CardStrokeColorDefaultBrush}' CornerRadius='8'><StackPanel Spacing='6'><TextBlock Text='{Binding Name}' Style='{StaticResource SubtitleTextBlockStyle}'/><TextBlock Text='{Binding Description}' TextWrapping='Wrap'/></StackPanel></Border></DataTemplate>");
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = _localization.GetString("ProjectKind_ChooseTitle"), Content = selection, PrimaryButtonText = _localization.GetString("Action_Continue"), CloseButtonText = _localization.GetString("Action_Cancel"), DefaultButton = ContentDialogButton.Primary, IsPrimaryButtonEnabled = false };
        selection.SelectionChanged += (_, _) => dialog.IsPrimaryButtonEnabled = selection.SelectedItem is not null;
        return await AppDialogService.Default.ShowAsync(dialog) == ContentDialogResult.Primary ? (selection.SelectedItem as KindChoice)?.Kind : null;
    }

    private async Task ShowCreateDialogAsync(string kind)
    {
        var isResearch = kind == ProjectKindCodes.Research;
        var name = CreateTextBox("Project_Field_Name", ProjectValidation.MaxNameLength);
        var type = new ComboBox { Header = _localization.GetString(isResearch ? "ResearchProject_Field_Type" : "Project_Field_Type"), DisplayMemberPath = "Name", HorizontalAlignment = HorizontalAlignment.Stretch };
        var codes = isResearch ? ResearchProjectTypeCodes.All : ProjectTypeCodes.All;
        type.ItemsSource = codes.Select(code => new ProjectTypeOption(code, isResearch ? ProjectPresentation.GetResearchTypeName(code, _localization) : ProjectPresentation.GetDesignTypeName(code, _localization))).ToArray();
        type.SelectedIndex = 0;
        var customType = CreateTextBox("Project_Field_CustomType", ProjectValidation.MaxTypeLength); customType.Visibility = Visibility.Collapsed;
        type.SelectionChanged += (_, _) => customType.Visibility = (type.SelectedItem as ProjectTypeOption)?.Code == ProjectTypeCodes.Other ? Visibility.Visible : Visibility.Collapsed;
        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(new TextBlock { Text = _localization.GetFormattedString("ProjectKind_Creating", ProjectPresentation.GetKindName(kind, _localization)), FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(name); panel.Children.Add(type); panel.Children.Add(customType);

        TextBox? area = null, latitude = null, longitude = null, description = null, requirements = null;
        TextBox? field = null, subject = null, methods = null;
        if (isResearch)
        {
            field = CreateTextBox("ResearchProject_Field_Field", ProjectValidation.MaxResearchFieldLength);
            subject = CreateTextBox("ResearchProject_Field_Subject", ProjectValidation.MaxResearchSubjectLength, true, 110);
            methods = CreateTextBox("ResearchProject_Field_Methods", ProjectValidation.MaxResearchMethodsLength, true, 110);
            panel.Children.Add(field); panel.Children.Add(subject); panel.Children.Add(methods);
        }
        else
        {
            area = CreateTextBox("Project_Field_AdministrativeArea", ProjectValidation.MaxAdministrativeAreaLength);
            latitude = CreateTextBox("Project_Field_Latitude", 40); longitude = CreateTextBox("Project_Field_Longitude", 40);
            description = CreateTextBox("Project_Field_Description", ProjectValidation.MaxDescriptionLength, true, 90);
            requirements = CreateTextBox("Project_Field_PlanningRequirements", ProjectValidation.MaxPlanningRequirementsLength, true, 110);
            panel.Children.Add(area);
            panel.Children.Add(new TextBlock { Text = _localization.GetString("Project_Coordinates_Wgs84_Label"), FontWeight = FontWeights.SemiBold });
            panel.Children.Add(latitude); panel.Children.Add(longitude); panel.Children.Add(description); panel.Children.Add(requirements);
        }
        var error = new TextBlock { Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"], TextWrapping = TextWrapping.Wrap };
        panel.Children.Add(error);
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = _localization.GetString(isResearch ? "Project_New_ResearchTitle" : "Project_New_DesignTitle"), Content = new ScrollViewer { Content = panel, MaxHeight = 580 }, PrimaryButtonText = _localization.GetString("Action_Create"), SecondaryButtonText = _localization.GetString("Action_Back"), CloseButtonText = _localization.GetString("Action_Cancel"), DefaultButton = ContentDialogButton.Primary };
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            args.Cancel = true; var deferral = args.GetDeferral();
            try
            {
                var selected = (ProjectTypeOption)type.SelectedItem;
                ProjectSaveResult result;
                if (isResearch)
                    result = await _projects.CreateResearchAsync(name.Text, selected.Code, customType.Text, field!.Text, subject!.Text, methods!.Text);
                else
                {
                    if (!TryParseCoordinate(latitude!.Text, out var lat) || !TryParseCoordinate(longitude!.Text, out var lon)) { error.Text = _localization.GetString("Project_Error_InvalidCoordinate"); return; }
                    result = await _projects.CreateAsync(name.Text, selected.Code, customType.Text, area!.Text, lat, lon, description!.Text, requirements!.Text);
                }
                if (!result.Succeeded) { error.Text = LocalizeValidation(result.ValidationErrors); return; }
                args.Cancel = false; _sessionKind = kind; await RefreshAsync(); Frame.Navigate(typeof(ProjectWorkspacePage), result.Project!.Id);
            }
            finally { deferral.Complete(); }
        };
        var dialogResult = await AppDialogService.Default.ShowAsync(dialog);
        if (dialogResult == ContentDialogResult.Secondary)
        {
            var newKind = await ChooseKindAsync();
            if (newKind is not null) await ShowCreateDialogAsync(newKind);
        }
    }

    private TextBox CreateTextBox(string key, int maxLength, bool multiline = false, double minHeight = 0)
    {
        var header = _localization.GetString(key);
        var box = new TextBox { Header = header, MaxLength = maxLength, AcceptsReturn = multiline, TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap, MinHeight = minHeight, HorizontalAlignment = HorizontalAlignment.Stretch };
        AutomationProperties.SetName(box, header);
        return box;
    }

    private void ConfigureButton(Button button, string key)
    {
        var text = _localization.GetString(key); button.Content = text; AutomationProperties.SetName(button, text); ToolTipService.SetToolTip(button, text);
    }

    private void OnOpenProject(object sender, RoutedEventArgs e) { if ((sender as Button)?.Tag is ProjectRecord project) Frame.Navigate(typeof(ProjectWorkspacePage), project.Id); }
    private string LocalizeValidation(IReadOnlyList<string>? errors) => errors is null ? _localization.GetString("Project_Error_SaveFailed") : string.Join(Environment.NewLine, errors.Select(error => _localization.GetString($"ProjectValidation_{error}")));

    private static bool TryParseCoordinate(string text, out decimal? value)
    {
        value = null; if (string.IsNullOrWhiteSpace(text)) return true;
        if (decimal.TryParse(text, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.CurrentCulture, out var parsed) || decimal.TryParse(text, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out parsed)) { value = parsed; return true; }
        return false;
    }

    private sealed record ProjectKindOption(string Id, string Kind, string DisplayName);
    private sealed record KindChoice(string Kind, string Name, string Description);
    private sealed record ProjectTypeOption(string Code, string Name);
    private sealed record ProjectCard(ProjectRecord Project, string Name, string Kind, string PrimaryDetails, string SecondaryDetails, string FullDetails, string Statistics, string Updated)
    {
        public ProjectCard(ProjectRecord project, ILocalizationService localization) : this(
            project, project.Name, ProjectPresentation.GetKindName(project.Kind, localization),
            project.Kind == ProjectKindCodes.Research
                ? string.Join(" · ", new[] { ProjectPresentation.GetTypeName(project, localization), project.ResearchDetails?.ResearchField }.Where(value => !string.IsNullOrWhiteSpace(value)))
                : string.Join(" · ", new[] { ProjectPresentation.GetTypeName(project, localization), project.AdministrativeArea }.Where(value => !string.IsNullOrWhiteSpace(value))),
            project.Kind == ProjectKindCodes.Research ? ProjectPresentation.CreateResearchSubjectSummary(project.ResearchDetails?.ResearchSubject) : string.Empty,
            project.Kind == ProjectKindCodes.Research ? project.ResearchDetails?.ResearchSubject ?? string.Empty : string.Empty,
            localization.GetFormattedString("Project_Card_Milestones", project.Milestones.Count),
            localization.GetFormattedString("Project_Card_Updated", project.UpdatedAtUtc.ToLocalTime().ToString("g"))) { }
    }
}
