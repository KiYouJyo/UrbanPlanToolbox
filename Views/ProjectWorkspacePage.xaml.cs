using System.Globalization;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using UrbanPlanToolbox.Models.Projects;
using UrbanPlanToolbox.Services;

namespace UrbanPlanToolbox.Views;

public sealed partial class ProjectWorkspacePage : Page
{
    private readonly ProjectStorageService _projects = ProjectStorageService.Default;
    private readonly IProjectFolderAccessService _folders = WindowsProjectFolderAccessService.Default;
    private readonly ILocalizationService _localization = LocalizationService.Default;
    private ProjectRecord? _project;
    private bool _applying;
    private bool _dirty;
    private bool _confirmedNavigation;
    private bool _busy;

    public ProjectWorkspacePage()
    {
        InitializeComponent();
        ConfigureUi();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is not Guid id) { ShowError("Project_Error_NotFound"); return; }
        var read = await _projects.ReadAsync(id);
        if (!read.HasValue) { ShowError("Project_Error_LoadFailed"); return; }
        _project = read.Value;
        ApplyProject();
    }

    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        if (!_dirty || _confirmedNavigation) { base.OnNavigatingFrom(e); return; }
        e.Cancel = true;
        _ = ConfirmDiscardAndNavigateAsync(e.SourcePageType, e.Parameter);
    }

    private async Task ConfirmDiscardAndNavigateAsync(Type sourcePageType, object parameter)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot, Title = _localization.GetString("Project_Unsaved_Title"),
            Content = _localization.GetString("Project_Unsaved_Message"),
            PrimaryButtonText = _localization.GetString("Action_Discard"),
            CloseButtonText = _localization.GetString("Action_Cancel"), DefaultButton = ContentDialogButton.Close
        };
        if (await AppDialogService.Default.ShowAsync(dialog) == ContentDialogResult.Primary)
        {
            _confirmedNavigation = true;
            Frame.Navigate(sourcePageType, parameter);
        }
    }

    private void ConfigureUi()
    {
        BasicInfoExpander.Header = _localization.GetString("ProjectWorkspace_BasicInformation");
        ResearchDetailsExpander.Header = _localization.GetString("ResearchProjectWorkspace_Details");
        DescriptionExpander.Header = _localization.GetString("ProjectWorkspace_Description");
        RequirementsExpander.Header = _localization.GetString("ProjectWorkspace_PlanningRequirements");
        MilestonesExpander.Header = _localization.GetString("ProjectWorkspace_Milestones");
        FolderExpander.Header = _localization.GetString("ProjectWorkspace_FolderLabel");
        ManagementExpander.Header = _localization.GetString("ProjectWorkspace_Management");
        CoordinatesLabel.Text = _localization.GetString("Project_Coordinates_Wgs84_Label");
        RequirementsHelpText.Text = _localization.GetString("Project_PlanningRequirements_Help");
        DeleteExplanationText.Text = _localization.GetString("Project_Delete_Explanation");
        LegacyDataNotice.Text = _localization.GetString("Project_LegacyData_Notice");

        NameBox.Header = _localization.GetString("Project_Field_Name"); NameBox.MaxLength = ProjectValidation.MaxNameLength;
        TypeBox.Header = _localization.GetString("Project_Field_Type");
        TypeBox.ItemsSource = ProjectTypeCodes.All.Select(code => new ProjectTypeOption(code, ProjectPresentation.GetTypeName(code, _localization))).ToArray();
        CustomTypeBox.Header = _localization.GetString("Project_Field_CustomType"); CustomTypeBox.MaxLength = ProjectValidation.MaxTypeLength;
        AreaBox.Header = _localization.GetString("Project_Field_AdministrativeArea"); AreaBox.MaxLength = ProjectValidation.MaxAdministrativeAreaLength;
        LatitudeBox.Header = _localization.GetString("Project_Field_Latitude"); LongitudeBox.Header = _localization.GetString("Project_Field_Longitude");
        DescriptionBox.Header = _localization.GetString("Project_Field_Description"); DescriptionBox.MaxLength = ProjectValidation.MaxDescriptionLength;
        PlanningRequirementsBox.Header = _localization.GetString("Project_Field_PlanningRequirements"); PlanningRequirementsBox.MaxLength = ProjectValidation.MaxPlanningRequirementsLength;
        ResearchFieldBox.Header = _localization.GetString("ResearchProject_Field_Field"); ResearchFieldBox.MaxLength = ProjectValidation.MaxResearchFieldLength;
        ResearchSubjectBox.Header = _localization.GetString("ResearchProject_Field_Subject"); ResearchSubjectBox.MaxLength = ProjectValidation.MaxResearchSubjectLength;
        ResearchMethodsBox.Header = _localization.GetString("ResearchProject_Field_Methods"); ResearchMethodsBox.MaxLength = ProjectValidation.MaxResearchMethodsLength;

        foreach (var control in new Control[] { NameBox, TypeBox, CustomTypeBox, AreaBox, LatitudeBox, LongitudeBox, DescriptionBox, PlanningRequirementsBox, ResearchFieldBox, ResearchSubjectBox, ResearchMethodsBox })
            AutomationProperties.SetName(control, control is TextBox text ? text.Header?.ToString() ?? string.Empty : ((ComboBox)control).Header?.ToString() ?? string.Empty);

        ConfigureButton(SaveButton, "Project_Action_Save");
        ConfigureButton(ResetButton, "Project_Action_Reset");
        ConfigureButton(ResearchSaveButton, "Project_Action_Save");
        ConfigureButton(ResearchResetButton, "Project_Action_Reset");
        ConfigureButton(AddMilestoneButton, "Milestone_Action_Add");
        ConfigureButton(SelectFolderButton, "Folder_Action_Select");
        ConfigureButton(OpenFolderButton, "Folder_Action_Open");
        ConfigureButton(ClearFolderButton, "Folder_Action_Clear");
        ConfigureButton(DeleteButton, "Project_Action_Delete");
        DeleteButton.Background = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"];
    }

    private void ConfigureButton(Button button, string key)
    {
        var text = _localization.GetString(key);
        button.Content = text;
        AutomationProperties.SetName(button, text);
        ToolTipService.SetToolTip(button, text);
    }

    private void ApplyProject()
    {
        if (_project is null) return;
        _applying = true;
        TitleText.Text = _project.Name;
        ToolTipService.SetToolTip(TitleText, _project.Name);
        MetadataText.Text = _localization.GetFormattedString("ProjectWorkspace_Metadata", ProjectPresentation.GetTypeName(_project, _localization), _project.UpdatedAtUtc.ToLocalTime().ToString("g"));
        MetadataText.Text = $"{ProjectPresentation.GetKindName(_project.Kind, _localization)} · {MetadataText.Text}";
        StateText.Text = _localization.GetString(_project.IsArchived ? "Project_State_Archived" : "Project_State_Active");
        StateBadge.Background = (Brush)Application.Current.Resources[_project.IsArchived ? "SystemFillColorCautionBackgroundBrush" : "SystemFillColorSuccessBackgroundBrush"];
        // Use the documented semantic fill brushes for foreground text. The
        // similarly named *TextBrush resources are not present in every
        // Windows App SDK resource dictionary and would crash page creation.
        StateText.Foreground = (Brush)Application.Current.Resources[_project.IsArchived ? "SystemFillColorCautionBrush" : "SystemFillColorSuccessBrush"];
        NameBox.Text = _project.Name;
        CustomTypeBox.Text = _project.CustomType ?? string.Empty;
        CustomTypeBox.Visibility = _project.Type == ProjectTypeCodes.Other ? Visibility.Visible : Visibility.Collapsed;
        var isResearch = _project.Kind == ProjectKindCodes.Research;
        TypeBox.Header = _localization.GetString(isResearch ? "ResearchProject_Field_Type" : "Project_Field_Type");
        TypeBox.ItemsSource = (isResearch ? ResearchProjectTypeCodes.All : ProjectTypeCodes.All)
            .Select(code => new ProjectTypeOption(code, isResearch ? ProjectPresentation.GetResearchTypeName(code, _localization) : ProjectPresentation.GetDesignTypeName(code, _localization))).ToArray();
        TypeBox.SelectedItem = TypeBox.Items.Cast<ProjectTypeOption>().First(item => item.Code == _project.Type);
        AreaBox.Text = _project.AdministrativeArea ?? string.Empty;
        LatitudeBox.Text = _project.Latitude?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;
        LongitudeBox.Text = _project.Longitude?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;
        DescriptionBox.Text = _project.Description ?? string.Empty;
        PlanningRequirementsBox.Text = _project.PlanningRequirements ?? string.Empty;
        ResearchFieldBox.Text = _project.ResearchDetails?.ResearchField ?? string.Empty;
        ResearchSubjectBox.Text = _project.ResearchDetails?.ResearchSubject ?? string.Empty;
        ResearchMethodsBox.Text = _project.ResearchDetails?.ResearchMethods ?? string.Empty;
        AreaBox.Visibility = isResearch ? Visibility.Collapsed : Visibility.Visible;
        CoordinatesLabel.Visibility = isResearch ? Visibility.Collapsed : Visibility.Visible;
        CoordinatesGrid.Visibility = isResearch ? Visibility.Collapsed : Visibility.Visible;
        DescriptionExpander.Visibility = isResearch ? Visibility.Collapsed : Visibility.Visible;
        RequirementsExpander.Visibility = isResearch ? Visibility.Collapsed : Visibility.Visible;
        ResearchDetailsExpander.Visibility = isResearch ? Visibility.Visible : Visibility.Collapsed;
        SetEditingEnabled(!_project.IsArchived);
        RenderMilestones();
        RenderFolder();
        RenderManagement();
        LegacyDataNotice.Visibility = _project.Todos.Count > 0 || _project.PlanningSnapshots.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        _dirty = false;
        _applying = false;
    }

    private void SetEditingEnabled(bool enabled)
    {
        foreach (var box in new[] { NameBox, CustomTypeBox, AreaBox, LatitudeBox, LongitudeBox, DescriptionBox, PlanningRequirementsBox, ResearchFieldBox, ResearchSubjectBox, ResearchMethodsBox }) box.IsReadOnly = !enabled;
        TypeBox.IsEnabled = enabled;
        SaveButton.IsEnabled = enabled && !_busy;
        ResetButton.IsEnabled = enabled && !_busy;
        ResearchSaveButton.IsEnabled = enabled && !_busy;
        ResearchResetButton.IsEnabled = enabled && !_busy;
        AddMilestoneButton.IsEnabled = enabled && !_busy;
        SelectFolderButton.IsEnabled = enabled && !_busy;
        ClearFolderButton.IsEnabled = enabled && !_busy && _project?.WorkFolder is not null;
        ArchiveButton.IsEnabled = !_busy;
        DeleteButton.IsEnabled = !_busy;
    }

    private void RenderMilestones()
    {
        MilestoneList.Children.Clear();
        if (_project is null) return;
        var milestones = _project.Milestones.OrderBy(item => item.Date).ThenBy(item => item.CreatedAtUtc).ThenBy(item => item.DisplayOrder).ToArray();
        foreach (var milestone in milestones)
        {
            var row = new Grid { ColumnSpacing = 12, Padding = new Thickness(12), HorizontalAlignment = HorizontalAlignment.Stretch };
            row.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            var details = new StackPanel { Spacing = 3 };
            details.Children.Add(new TextBlock { Text = milestone.Title, Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"], TextWrapping = TextWrapping.Wrap });
            var date = milestone.Date.ToDateTime(milestone.Time ?? TimeOnly.MinValue);
            var when = milestone.Time.HasValue ? date.ToString("g", CultureInfo.CurrentCulture) : date.ToString("d", CultureInfo.CurrentCulture);
            details.Children.Add(new TextBlock { Text = when, TextWrapping = TextWrapping.Wrap });
            if (!string.IsNullOrWhiteSpace(milestone.Notes)) details.Children.Add(new TextBlock { Text = milestone.Notes, TextWrapping = TextWrapping.Wrap });
            row.Children.Add(details);
            var actions = new StackPanel { Orientation = Orientation.Vertical, Spacing = 6 };
            var edit = new Button { Tag = milestone.Id, IsEnabled = !_project.IsArchived && !_busy };
            ConfigureButton(edit, "Milestone_Action_Edit"); edit.Click += OnEditMilestone;
            var delete = new Button { Tag = milestone.Id, IsEnabled = !_project.IsArchived && !_busy };
            ConfigureButton(delete, "Milestone_Action_Delete"); delete.Click += OnDeleteMilestone;
            actions.Children.Add(edit); actions.Children.Add(delete); Grid.SetColumn(actions, 1); row.Children.Add(actions);
            MilestoneList.Children.Add(row);
        }
        MilestoneEmptyText.Text = _localization.GetString("Milestone_Empty");
        MilestoneEmptyText.Visibility = milestones.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RenderFolder()
    {
        if (_project?.WorkFolder is null) FolderText.Text = _localization.GetString("ProjectFolder_None");
        else if (_project.WorkFolder.RequiresReselection) FolderText.Text = _localization.GetFormattedString("ProjectFolder_ReselectionSummary", _project.WorkFolder.DisplayName, _project.WorkFolder.DisplayPath);
        else FolderText.Text = _localization.GetFormattedString("ProjectFolder_Summary", _project.WorkFolder.DisplayName, _project.WorkFolder.DisplayPath);
        ConfigureButton(SelectFolderButton, _project?.WorkFolder is null ? "Folder_Action_Select" : "Folder_Action_Replace");
        OpenFolderButton.IsEnabled = !_busy && _project?.WorkFolder is { RequiresReselection: false };
        ClearFolderButton.IsEnabled = !_busy && _project is { IsArchived: false, WorkFolder: not null };
    }

    private void RenderManagement()
    {
        if (_project is null) return;
        ManagementStateText.Text = _localization.GetFormattedString("Project_Management_CurrentState", _localization.GetString(_project.IsArchived ? "Project_State_Archived" : "Project_State_Active"));
        ArchiveDetailsText.Text = _project.IsArchived
            ? _localization.GetFormattedString("Project_Management_ArchivedAt", _project.ArchivedAtUtc?.ToLocalTime().ToString("g") ?? "—")
            : _localization.GetString("Project_Archive_Explanation");
        ConfigureButton(ArchiveButton, _project.IsArchived ? "Project_Action_Restore" : "Project_Action_Archive");
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        if (_project is null || !TryCreateCandidate(out var candidate)) return;
        SetBusy(true);
        try
        {
            candidate.UpdatedAtUtc = DateTimeOffset.UtcNow;
            var result = await _projects.SaveAsync(candidate);
            if (!result.Succeeded) { ShowValidation(result.ValidationErrors); return; }
            _project = result.Project;
            ShowSuccess("Project_Status_Saved");
            ApplyProject();
        }
        finally { SetBusy(false); }
    }

    private bool TryCreateCandidate(out ProjectRecord candidate)
    {
        candidate = null!;
        if (_project is null || _project.Kind == ProjectKindCodes.Design && (!TryParseCoordinate(LatitudeBox.Text, out _) || !TryParseCoordinate(LongitudeBox.Text, out _)))
        { ShowError("Project_Error_InvalidCoordinate"); return false; }
        candidate = JsonSerializer.Deserialize<ProjectRecord>(JsonSerializer.Serialize(_project, DataStorageJson.Options), DataStorageJson.Options)!;
        candidate.Name = NameBox.Text;
        candidate.Type = (TypeBox.SelectedItem as ProjectTypeOption)?.Code ?? string.Empty;
        candidate.CustomType = candidate.Type == ProjectTypeCodes.Other ? CustomTypeBox.Text : null;
        if (candidate.Kind == ProjectKindCodes.Design)
        {
            TryParseCoordinate(LatitudeBox.Text, out var latitude); TryParseCoordinate(LongitudeBox.Text, out var longitude);
            candidate.AdministrativeArea = AreaBox.Text; candidate.Latitude = latitude; candidate.Longitude = longitude;
            candidate.Description = DescriptionBox.Text; candidate.PlanningRequirements = PlanningRequirementsBox.Text;
        }
        else
        {
            candidate.ResearchDetails!.ResearchField = ResearchFieldBox.Text;
            candidate.ResearchDetails.ResearchSubject = ResearchSubjectBox.Text;
            candidate.ResearchDetails.ResearchMethods = ResearchMethodsBox.Text;
        }
        var errors = ProjectValidation.Validate(candidate);
        if (errors.Count == 0) return true;
        ShowValidation(errors);
        return false;
    }

    private void OnReset(object sender, RoutedEventArgs e) => ApplyProject();

    private async void OnAddMilestone(object sender, RoutedEventArgs e)
    {
        if (_project is null || !EnsureNoPendingEdits()) return;
        var input = await ShowMilestoneDialogAsync(null);
        if (input is null) return;
        SetBusy(true);
        try { await ApplyMutationAsync(await _projects.AddMilestoneAsync(_project.Id, input.Title, input.Date, input.Time, input.Notes)); }
        finally { SetBusy(false); }
    }

    private async void OnEditMilestone(object sender, RoutedEventArgs e)
    {
        if (_project is null || sender is not Button button || button.Tag is not Guid id || !EnsureNoPendingEdits()) return;
        var milestone = _project.Milestones.FirstOrDefault(item => item.Id == id);
        if (milestone is null) return;
        var input = await ShowMilestoneDialogAsync(milestone);
        if (input is null) return;
        SetBusy(true);
        try { await ApplyMutationAsync(await _projects.UpdateMilestoneAsync(_project.Id, id, input.Title, input.Date, input.Time, input.Notes)); }
        finally { SetBusy(false); }
    }

    private async void OnDeleteMilestone(object sender, RoutedEventArgs e)
    {
        if (_project is null || sender is not Button button || button.Tag is not Guid id || !EnsureNoPendingEdits() ||
            !await ConfirmAsync("Milestone_Delete_Title", "Milestone_Delete_Message")) return;
        SetBusy(true);
        try { await ApplyMutationAsync(await _projects.DeleteMilestoneAsync(_project.Id, id)); }
        finally { SetBusy(false); }
    }

    private async Task<MilestoneEditor?> ShowMilestoneDialogAsync(ProjectMilestone? milestone)
    {
        var title = new TextBox { Header = _localization.GetString("Milestone_Field_Title"), MaxLength = ProjectValidation.MaxMilestoneTitleLength, Text = milestone?.Title ?? string.Empty };
        var date = new CalendarDatePicker { Header = _localization.GetString("Milestone_Field_Date"), Date = milestone is null ? null : new DateTimeOffset(milestone.Date.ToDateTime(TimeOnly.MinValue)) };
        var includeTime = new CheckBox { Content = _localization.GetString("Milestone_Field_IncludeTime"), IsChecked = milestone?.Time is not null };
        var time = new TimePicker { Header = _localization.GetString("Milestone_Field_Time"), SelectedTime = milestone?.Time?.ToTimeSpan(), Visibility = includeTime.IsChecked == true ? Visibility.Visible : Visibility.Collapsed };
        includeTime.Click += (_, _) => time.Visibility = includeTime.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        var notes = new TextBox { Header = _localization.GetString("Milestone_Field_Notes"), MaxLength = ProjectValidation.MaxMilestoneNotesLength, Text = milestone?.Notes ?? string.Empty, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 100 };
        var error = new TextBlock { Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"], TextWrapping = TextWrapping.Wrap };
        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(title); panel.Children.Add(date); panel.Children.Add(includeTime); panel.Children.Add(time); panel.Children.Add(notes); panel.Children.Add(error);
        MilestoneEditor? result = null;
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot, Title = _localization.GetString(milestone is null ? "Milestone_Add_Title" : "Milestone_Edit_Title"), Content = panel,
            PrimaryButtonText = _localization.GetString(milestone is null ? "Action_Create" : "Project_Action_Save"), CloseButtonText = _localization.GetString("Action_Cancel"), DefaultButton = ContentDialogButton.Primary
        };
        dialog.PrimaryButtonClick += (_, args) =>
        {
            if (string.IsNullOrWhiteSpace(title.Text)) { args.Cancel = true; error.Text = _localization.GetString("ProjectValidation_MilestoneTitleRequired"); return; }
            if (date.Date is null) { args.Cancel = true; error.Text = _localization.GetString("ProjectValidation_MilestoneDateInvalid"); return; }
            if (includeTime.IsChecked == true && time.SelectedTime is null) { args.Cancel = true; error.Text = _localization.GetString("Milestone_Error_TimeRequired"); return; }
            result = new(title.Text, DateOnly.FromDateTime(date.Date.Value.LocalDateTime), includeTime.IsChecked == true ? TimeOnly.FromTimeSpan(time.SelectedTime!.Value) : null, notes.Text);
        };
        return await AppDialogService.Default.ShowAsync(dialog) == ContentDialogResult.Primary ? result : null;
    }

    private async void OnSelectFolder(object sender, RoutedEventArgs e)
    {
        if (_project is null || !EnsureNoPendingEdits()) return;
        var previous = _project.WorkFolder;
        var selected = await _folders.SelectAsync(_project.Id, previous);
        if (!selected.Succeeded)
        {
            if (selected.ErrorKey != "ProjectFolder_SelectionCancelled") ShowError(selected.ErrorKey ?? "Project_Error_SaveFailed");
            return;
        }
        _project.WorkFolder = selected.Reference;
        _project.UpdatedAtUtc = DateTimeOffset.UtcNow;
        SetBusy(true);
        try
        {
            var result = await _projects.SaveAsync(_project);
            if (result.Succeeded) _folders.Clear(previous);
            else { _folders.Clear(selected.Reference); _project.WorkFolder = previous; }
            await ApplyMutationAsync(result);
        }
        finally { SetBusy(false); }
    }

    private async void OnOpenFolder(object sender, RoutedEventArgs e)
    {
        if (_project?.WorkFolder is null) return;
        var result = await _folders.OpenAsync(_project.WorkFolder);
        if (!result.Succeeded) ShowError(result.ErrorKey ?? "ProjectFolder_OpenFailed");
    }

    private async void OnClearFolder(object sender, RoutedEventArgs e)
    {
        if (_project is null || !EnsureNoPendingEdits()) return;
        var previous = _project.WorkFolder;
        _project.WorkFolder = null;
        _project.UpdatedAtUtc = DateTimeOffset.UtcNow;
        SetBusy(true);
        try
        {
            var result = await _projects.SaveAsync(_project);
            if (result.Succeeded) _folders.Clear(previous); else _project.WorkFolder = previous;
            await ApplyMutationAsync(result);
        }
        finally { SetBusy(false); }
    }

    private async void OnArchive(object sender, RoutedEventArgs e)
    {
        if (_project is null || !EnsureNoPendingEdits()) return;
        var wasArchived = _project.IsArchived;
        if (!await ConfirmAsync(wasArchived ? "Project_Restore_Title" : "Project_Archive_Title", wasArchived ? "Project_Restore_Message" : "Project_Archive_Message")) return;
        SetBusy(true);
        try
        {
            var result = await _projects.ArchiveAsync(_project.Id, !wasArchived);
            if (!result.Succeeded) { ShowError("Project_Error_SaveFailed"); return; }
            _confirmedNavigation = true;
            Frame.Navigate(wasArchived ? typeof(HomePage) : typeof(ProjectArchivePage));
        }
        finally { SetBusy(false); }
    }

    private async void OnDeleteProject(object sender, RoutedEventArgs e)
    {
        if (_project is null || !await ConfirmPermanentDeleteAsync(_project.Name)) return;
        var wasArchived = _project.IsArchived;
        SetBusy(true);
        try
        {
            var result = await _projects.DeleteAsync(_project.Id, _folders);
            if (!result.Succeeded) { ShowError("Project_Delete_Failed"); return; }
            _confirmedNavigation = true;
            Frame.Navigate(wasArchived ? typeof(ProjectArchivePage) : typeof(HomePage));
        }
        finally { SetBusy(false); }
    }

    private async Task<bool> ConfirmPermanentDeleteAsync(string projectName)
    {
        var warning = new TextBlock { Text = _localization.GetFormattedString("Project_Delete_Warning", projectName), TextWrapping = TextWrapping.Wrap };
        var confirmation = new TextBox { Header = _localization.GetString("Project_Delete_ConfirmName"), PlaceholderText = projectName };
        var external = new TextBlock { Text = _localization.GetString("Project_Delete_ExternalFolderSafe"), TextWrapping = TextWrapping.Wrap };
        var panel = new StackPanel { Spacing = 10 }; panel.Children.Add(warning); panel.Children.Add(confirmation); panel.Children.Add(external);
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot, Title = _localization.GetString("Project_Delete_Title"), Content = panel,
            PrimaryButtonText = _localization.GetString("Project_Action_Delete"), CloseButtonText = _localization.GetString("Action_Cancel"),
            DefaultButton = ContentDialogButton.Close, IsPrimaryButtonEnabled = false
        };
        confirmation.TextChanged += (_, _) => dialog.IsPrimaryButtonEnabled = ProjectValidation.MatchesDeleteConfirmation(projectName, confirmation.Text);
        return await AppDialogService.Default.ShowAsync(dialog) == ContentDialogResult.Primary;
    }

    private bool EnsureNoPendingEdits()
    {
        if (!_dirty) return true;
        ShowError("Project_Unsaved_SaveBeforeAction");
        return false;
    }

    private void OnBack(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(HomePage));
    private void OnFieldChanged(object sender, TextChangedEventArgs e) { if (!_applying) _dirty = true; }
    private void OnTypeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TypeBox.SelectedItem is ProjectTypeOption option) CustomTypeBox.Visibility = option.Code == ProjectTypeCodes.Other ? Visibility.Visible : Visibility.Collapsed;
        if (!_applying) _dirty = true;
    }

    private async Task ApplyMutationAsync(ProjectSaveResult result)
    {
        if (!result.Succeeded) { ShowValidation(result.ValidationErrors); return; }
        _project = result.Project;
        ShowSuccess("Project_Status_Saved");
        ApplyProject();
        await Task.CompletedTask;
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        if (_project is not null) SetEditingEnabled(!_project.IsArchived);
        RenderMilestones();
        RenderFolder();
    }

    private async Task<bool> ConfirmAsync(string titleKey, string messageKey)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot, Title = _localization.GetString(titleKey), Content = _localization.GetString(messageKey),
            PrimaryButtonText = _localization.GetString("Action_Confirm"), CloseButtonText = _localization.GetString("Action_Cancel"), DefaultButton = ContentDialogButton.Close
        };
        return await AppDialogService.Default.ShowAsync(dialog) == ContentDialogResult.Primary;
    }

    private void ShowValidation(IReadOnlyList<string>? errors)
    {
        StatusBar.Severity = InfoBarSeverity.Error;
        StatusBar.Message = errors is null ? _localization.GetString("Project_Error_SaveFailed") : string.Join(Environment.NewLine, errors.Select(error => _localization.GetString($"ProjectValidation_{error}")));
        StatusBar.IsOpen = true;
    }
    private void ShowError(string key)
    {
        var message = _localization.GetString(key);
        StatusBar.Severity = InfoBarSeverity.Error; StatusBar.Message = message; StatusBar.IsOpen = true;
        AppNotificationService.Default.Notify(new(UrbanPlanToolbox.Models.Interaction.AppNotificationKind.Error, _localization.GetString("Interaction_ErrorTitle"), message, true));
    }
    private void ShowSuccess(string key)
    {
        var message = _localization.GetString(key);
        StatusBar.Severity = InfoBarSeverity.Success; StatusBar.Message = message; StatusBar.IsOpen = true;
        AppNotificationService.Default.Notify(new(UrbanPlanToolbox.Models.Interaction.AppNotificationKind.Success, _localization.GetString("Interaction_SuccessTitle"), message));
    }

    private static bool TryParseCoordinate(string text, out decimal? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text)) return true;
        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out var parsed) ||
            decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out parsed)) { value = parsed; return true; }
        return false;
    }

    private sealed record ProjectTypeOption(string Code, string Name);
    private sealed record MilestoneEditor(string Title, DateOnly Date, TimeOnly? Time, string? Notes);
}
