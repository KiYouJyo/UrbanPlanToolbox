using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using UrbanPlanToolbox.Controls;
using UrbanPlanToolbox.Models.Projects;
using UrbanPlanToolbox.Services;

namespace UrbanPlanToolbox.Views;

public sealed partial class HomePage : Page
{
    public const string DesignCategoryId = "design-projects";
    public const string ResearchCategoryId = "research-projects";
    private static string _sessionKind = ProjectKindCodes.Design;
    private readonly ProjectStorageService _projects = ProjectStorageService.Default;
    private readonly IProjectFolderAccessService _folders = WindowsProjectFolderAccessService.Default;
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
        selection.ItemTemplate = (DataTemplate)XamlReader.Load("<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><Border Padding='16' Margin='0,0,0,8' BorderThickness='0' CornerRadius='8'><StackPanel Spacing='6'><TextBlock Text='{Binding Name}' Style='{StaticResource SubtitleTextBlockStyle}'/><TextBlock Text='{Binding Description}' TextWrapping='Wrap'/></StackPanel></Border></DataTemplate>");
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = _localization.GetString("ProjectKind_ChooseTitle"), Content = selection, PrimaryButtonText = _localization.GetString("Action_Continue"), CloseButtonText = _localization.GetString("Action_Cancel"), DefaultButton = ContentDialogButton.Primary, IsPrimaryButtonEnabled = false };
        selection.SelectionChanged += (_, _) => dialog.IsPrimaryButtonEnabled = selection.SelectedItem is not null;
        return await AppDialogService.Default.ShowAsync(dialog) == ContentDialogResult.Primary ? (selection.SelectedItem as KindChoice)?.Kind : null;
    }

    private async Task ShowCreateDialogAsync(string kind)
    {
        var isResearch = kind == ProjectKindCodes.Research;
        var name = CreateTextBox("Project_Field_Name", ProjectValidation.MaxNameLength);
        var type = new ComboBox
        {
            Header = _localization.GetString(isResearch ? "ResearchProject_Field_Type" : "Project_Field_Type"),
            DisplayMemberPath = "Name",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        TransientComboBoxTheme.ApplyTo(type);
        var codes = isResearch ? ResearchProjectTypeCodes.All : ProjectTypeCodes.All;
        type.ItemsSource = codes
            .Select(code => new ProjectTypeOption(code, isResearch
                ? ProjectPresentation.GetResearchTypeName(code, _localization)
                : ProjectPresentation.GetDesignTypeName(code, _localization)))
            .ToArray();
        type.SelectedIndex = 0;

        var customType = CreateTextBox("Project_Field_CustomType", ProjectValidation.MaxTypeLength);
        customType.Visibility = Visibility.Collapsed;
        type.SelectionChanged += (_, _) =>
            customType.Visibility = (type.SelectedItem as ProjectTypeOption)?.Code == ProjectTypeCodes.Other
                ? Visibility.Visible
                : Visibility.Collapsed;

        var error = new TextBlock
        {
            Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
            TextWrapping = TextWrapping.Wrap
        };

        // Creating a project now captures identity only. Detailed design/research fields are
        // completed in Project Workspace, where they can be edited in context without a long modal form.
        var panel = new StackPanel
        {
            Spacing = 12,
            MinWidth = 420,
            MaxWidth = 520
        };
        panel.Children.Add(name);
        panel.Children.Add(type);
        panel.Children.Add(customType);
        panel.Children.Add(error);

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = _localization.GetString(isResearch ? "Project_New_ResearchTitle" : "Project_New_DesignTitle"),
            Content = panel,
            PrimaryButtonText = _localization.GetString("Action_Create"),
            SecondaryButtonText = _localization.GetString("Action_Back"),
            CloseButtonText = _localization.GetString("Action_Cancel"),
            DefaultButton = ContentDialogButton.Primary
        };

        dialog.PrimaryButtonClick += async (_, args) =>
        {
            args.Cancel = true;
            var deferral = args.GetDeferral();
            try
            {
                var selected = (ProjectTypeOption)type.SelectedItem;
                var result = isResearch
                    ? await _projects.CreateResearchAsync(name.Text, selected.Code, customType.Text, null, null, null)
                    : await _projects.CreateAsync(name.Text, selected.Code, customType.Text);

                if (!result.Succeeded)
                {
                    error.Text = LocalizeValidation(result.ValidationErrors);
                    return;
                }

                args.Cancel = false;
                _sessionKind = kind;
                await RefreshAsync();
                Frame.Navigate(typeof(ProjectWorkspacePage), result.Project!.Id);
            }
            finally
            {
                deferral.Complete();
            }
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

    private void OnOpenProject(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is Button source && !ReferenceEquals(source, sender)) return;
        if ((sender as Button)?.Tag is ProjectRecord project) Frame.Navigate(typeof(ProjectWorkspacePage), project.Id);
    }
    private async void OnOpenWorkFolder(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not ProjectRecord { WorkFolder: { RequiresReselection: false } folder }) return;
        var result = await _folders.OpenAsync(folder);
        if (!result.Succeeded)
        {
            StatusBar.IsOpen = true;
            StatusBar.Severity = InfoBarSeverity.Error;
            StatusBar.Message = _localization.GetString(result.ErrorKey ?? "ProjectFolder_OpenFailed");
        }
    }
    private string LocalizeValidation(IReadOnlyList<string>? errors) => errors is null ? _localization.GetString("Project_Error_SaveFailed") : string.Join(Environment.NewLine, errors.Select(error => _localization.GetString($"ProjectValidation_{error}")));

    private sealed record ProjectKindOption(string Id, string Kind, string DisplayName);
    private sealed record KindChoice(string Kind, string Name, string Description);
    private sealed record ProjectTypeOption(string Code, string Name);
    private sealed record ProjectCard(ProjectRecord Project, string Name, string Kind, string PrimaryDetails, string SecondaryDetails, string FullDetails, string Statistics, string Updated, bool HasWorkFolder, string WorkFolderActionText)
    {
        public ProjectCard(ProjectRecord project, ILocalizationService localization) : this(
            project, project.Name, ProjectPresentation.GetKindName(project.Kind, localization),
            project.Kind == ProjectKindCodes.Research
                ? string.Join(" · ", new[] { ProjectPresentation.GetTypeName(project, localization), project.ResearchDetails?.ResearchField }.Where(value => !string.IsNullOrWhiteSpace(value)))
                : string.Join(" · ", new[] { ProjectPresentation.GetTypeName(project, localization), project.AdministrativeArea }.Where(value => !string.IsNullOrWhiteSpace(value))),
            project.Kind == ProjectKindCodes.Research ? ProjectPresentation.CreateResearchSubjectSummary(project.ResearchDetails?.ResearchSubject) : string.Empty,
            project.Kind == ProjectKindCodes.Research ? project.ResearchDetails?.ResearchSubject ?? string.Empty : string.Empty,
            localization.GetFormattedString("Project_Card_Milestones", project.Milestones.Count),
            localization.GetFormattedString("Project_Card_Updated", project.UpdatedAtUtc.ToLocalTime().ToString("g")),
            project.WorkFolder is { RequiresReselection: false },
            localization.GetString(project.WorkFolder is { RequiresReselection: false } ? "ProjectFolder_Action_OpenWorkFolder" : "ProjectFolder_Action_NotLinked")) { }
    }
}