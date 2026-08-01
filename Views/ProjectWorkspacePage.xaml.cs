using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using UrbanPlanToolbox.Models.Projects;
using UrbanPlanToolbox.Models.Tools;
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

    public ProjectWorkspacePage()
    {
        InitializeComponent();
        ConfigureFields();
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
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = _localization.GetString("Project_Unsaved_Title"), Content = _localization.GetString("Project_Unsaved_Message"), PrimaryButtonText = _localization.GetString("Action_Discard"), CloseButtonText = _localization.GetString("Action_Cancel"), DefaultButton = ContentDialogButton.Close };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            _confirmedNavigation = true;
            Frame.Navigate(sourcePageType, parameter);
        }
    }

    private void ConfigureFields()
    {
        NameBox.Header = _localization.GetString("Project_Field_Name"); NameBox.MaxLength = ProjectValidation.MaxNameLength;
        TypeBox.Header = _localization.GetString("Project_Field_Type");
        TypeBox.ItemsSource = ProjectTypeCodes.All.Select(code => new ProjectTypeOption(code, ProjectPresentation.GetTypeName(code, _localization))).ToArray();
        CustomTypeBox.Header = _localization.GetString("Project_Field_CustomType"); CustomTypeBox.MaxLength = ProjectValidation.MaxTypeLength;
        AreaBox.Header = _localization.GetString("Project_Field_AdministrativeArea"); AreaBox.MaxLength = ProjectValidation.MaxAdministrativeAreaLength;
        LatitudeBox.Header = _localization.GetString("Project_Field_Latitude"); LongitudeBox.Header = _localization.GetString("Project_Field_Longitude");
        DescriptionBox.Header = _localization.GetString("Project_Field_Description"); DescriptionBox.MaxLength = ProjectValidation.MaxDescriptionLength;
        NewTodoBox.Header = _localization.GetString("Todo_Field_Title"); NewTodoBox.MaxLength = ProjectValidation.MaxTodoTitleLength;
        foreach (var control in new Control[] { NameBox, TypeBox, CustomTypeBox, AreaBox, LatitudeBox, LongitudeBox, DescriptionBox, NewTodoBox })
            AutomationProperties.SetName(control, control is TextBox text ? text.Header?.ToString() ?? string.Empty : ((ComboBox)control).Header?.ToString() ?? string.Empty);
    }

    private void ApplyProject()
    {
        if (_project is null) return;
        _applying = true;
        TitleText.Text = _project.Name;
        MetadataText.Text = _localization.GetFormattedString("ProjectWorkspace_Metadata", ProjectPresentation.GetTypeName(_project, _localization), _project.UpdatedAtUtc.ToLocalTime().ToString("g"));
        StateText.Text = _localization.GetString(_project.IsArchived ? "Project_State_Archived" : "Project_State_Active");
        StateBadge.Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[_project.IsArchived ? "SystemFillColorCautionBackgroundBrush" : "SystemFillColorSuccessBackgroundBrush"];
        NameBox.Text = _project.Name;
        TypeBox.SelectedItem = TypeBox.Items.Cast<ProjectTypeOption>().First(item => item.Code == _project.Type);
        CustomTypeBox.Text = _project.CustomType ?? string.Empty;
        CustomTypeBox.Visibility = _project.Type == ProjectTypeCodes.Other ? Visibility.Visible : Visibility.Collapsed;
        AreaBox.Text = _project.AdministrativeArea ?? string.Empty;
        LatitudeBox.Text = _project.Latitude?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;
        LongitudeBox.Text = _project.Longitude?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;
        DescriptionBox.Text = _project.Description ?? string.Empty;
        SetEditingEnabled(!_project.IsArchived);
        RenderTodos(); RenderSnapshots(); RenderFolder();
        ArchiveButton.Content = _localization.GetString(_project.IsArchived ? "Project_Action_Restore" : "Project_Action_Archive");
        ArchiveExplanation.Text = _localization.GetString(_project.IsArchived ? "Project_Restore_Explanation" : "Project_Archive_Explanation");
        _dirty = false; _applying = false;
    }

    private void SetEditingEnabled(bool enabled)
    {
        foreach (var control in new Control[] { NameBox, TypeBox, CustomTypeBox, AreaBox, LatitudeBox, LongitudeBox, DescriptionBox, SaveButton, NewTodoBox, AddTodoButton }) control.IsEnabled = enabled;
        OpenCalculatorButton.IsEnabled = enabled; SelectFolderButton.IsEnabled = enabled; ClearFolderButton.IsEnabled = enabled;
    }

    private void RenderTodos()
    {
        TodoList.Children.Clear();
        if (_project is null) return;
        foreach (var todo in _project.Todos.OrderBy(item => item.DisplayOrder))
        {
            var row = new Grid { ColumnSpacing = 8, Tag = todo.Id };
            row.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); row.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            var check = new CheckBox { IsChecked = todo.IsCompleted, IsEnabled = !_project.IsArchived, Tag = todo.Id, VerticalAlignment = VerticalAlignment.Center };
            AutomationProperties.SetName(check, _localization.GetFormattedString("Todo_Accessibility_Complete", todo.Title));
            check.Click += OnTodoChecked;
            var title = new TextBox { Text = todo.Title, IsReadOnly = _project.IsArchived, Tag = todo.Id, MaxLength = ProjectValidation.MaxTodoTitleLength };
            title.LostFocus += OnTodoTitleLostFocus; Grid.SetColumn(title, 1);
            var delete = new Button { Content = _localization.GetString("Action_Delete"), Tag = todo.Id, IsEnabled = !_project.IsArchived };
            delete.Click += OnDeleteTodo; Grid.SetColumn(delete, 2);
            row.Children.Add(check); row.Children.Add(title); row.Children.Add(delete); TodoList.Children.Add(row);
        }
        TodoEmptyText.Visibility = _project.Todos.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RenderSnapshots()
    {
        SnapshotList.Children.Clear();
        if (_project is null) return;
        foreach (var snapshot in _project.PlanningSnapshots.OrderByDescending(item => item.CreatedAtUtc))
        {
            var row = new Grid { ColumnSpacing = 8 };
            row.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            var result = snapshot.Result;
            var text = _localization.GetFormattedString("Snapshot_Summary", snapshot.Name ?? snapshot.CreatedAtUtc.ToLocalTime().ToString("g"), Format(result.FloorAreaRatio), Format(result.BuildingDensity), Format(result.GreenRatio));
            row.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center });
            var delete = new Button { Content = _localization.GetString("Action_Delete"), Tag = snapshot.Id, IsEnabled = !_project.IsArchived }; delete.Click += OnDeleteSnapshot; Grid.SetColumn(delete, 1); row.Children.Add(delete);
            SnapshotList.Children.Add(row);
        }
        SnapshotEmptyText.Visibility = _project.PlanningSnapshots.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RenderFolder()
    {
        if (_project?.WorkFolder is null) FolderText.Text = _localization.GetString("ProjectFolder_None");
        else if (_project.WorkFolder.RequiresReselection) FolderText.Text = _localization.GetFormattedString("ProjectFolder_ReselectionSummary", _project.WorkFolder.DisplayName, _project.WorkFolder.DisplayPath);
        else FolderText.Text = _localization.GetFormattedString("ProjectFolder_Summary", _project.WorkFolder.DisplayName, _project.WorkFolder.DisplayPath);
        OpenFolderButton.IsEnabled = _project?.WorkFolder is { RequiresReselection: false };
        ClearFolderButton.IsEnabled = _project is { IsArchived: false, WorkFolder: not null };
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        if (_project is null || !TryApplyEditor()) return;
        _project.UpdatedAtUtc = DateTimeOffset.UtcNow;
        var result = await _projects.SaveAsync(_project);
        if (!result.Succeeded) { ShowValidation(result.ValidationErrors); return; }
        ShowSuccess("Project_Status_Saved"); ApplyProject();
    }

    private bool TryApplyEditor()
    {
        if (_project is null || !TryParseCoordinate(LatitudeBox.Text, out var latitude) || !TryParseCoordinate(LongitudeBox.Text, out var longitude)) { ShowError("Project_Error_InvalidCoordinate"); return false; }
        _project.Name = NameBox.Text; _project.Type = ((ProjectTypeOption)TypeBox.SelectedItem).Code;
        _project.CustomType = CustomTypeBox.Text; _project.AdministrativeArea = AreaBox.Text; _project.Latitude = latitude; _project.Longitude = longitude; _project.Description = DescriptionBox.Text;
        var errors = ProjectValidation.Validate(_project); if (errors.Count > 0) { ShowValidation(errors); return false; }
        return true;
    }

    private async void OnAddTodo(object sender, RoutedEventArgs e) { if (_project is null) return; var result = await _projects.AddTodoAsync(_project.Id, NewTodoBox.Text); await ApplyMutationAsync(result); if (result.Succeeded) NewTodoBox.Text = string.Empty; }
    private async void OnTodoChecked(object sender, RoutedEventArgs e) { if (_project is null || sender is not CheckBox box) return; await ApplyMutationAsync(await _projects.UpdateTodoAsync(_project.Id, (Guid)box.Tag, isCompleted: box.IsChecked == true)); }
    private async void OnTodoTitleLostFocus(object sender, RoutedEventArgs e) { if (_project is null || sender is not TextBox box) return; var existing = _project.Todos.First(item => item.Id == (Guid)box.Tag); if (box.Text == existing.Title) return; await ApplyMutationAsync(await _projects.UpdateTodoAsync(_project.Id, existing.Id, box.Text)); }
    private async void OnDeleteTodo(object sender, RoutedEventArgs e)
    {
        if (_project is null || sender is not Button button || !await ConfirmAsync("Todo_Delete_Title", "Todo_Delete_Message")) return;
        await ApplyMutationAsync(await _projects.DeleteTodoAsync(_project.Id, (Guid)button.Tag));
    }
    private async void OnDeleteSnapshot(object sender, RoutedEventArgs e) { if (_project is null || sender is not Button button || !await ConfirmAsync("Snapshot_Delete_Title", "Snapshot_Delete_Message")) return; await ApplyMutationAsync(await _projects.DeleteSnapshotAsync(_project.Id, (Guid)button.Tag)); }
    private void OnOpenCalculator(object sender, RoutedEventArgs e) { if (_project is not null) { _confirmedNavigation = true; Frame.Navigate(typeof(PlanningCalculatorPage), _project.Id); } }

    private async void OnSelectFolder(object sender, RoutedEventArgs e)
    {
        if (_project is null) return;
        var selected = await _folders.SelectAsync(_project.Id, _project.WorkFolder); if (!selected.Succeeded) return;
        _project.WorkFolder = selected.Reference; _project.UpdatedAtUtc = DateTimeOffset.UtcNow; await ApplyMutationAsync(await _projects.SaveAsync(_project));
    }
    private async void OnOpenFolder(object sender, RoutedEventArgs e) { if (_project?.WorkFolder is null) return; var result = await _folders.OpenAsync(_project.WorkFolder); if (!result.Succeeded) ShowError(result.ErrorKey ?? "ProjectFolder_OpenFailed"); }
    private async void OnClearFolder(object sender, RoutedEventArgs e)
    {
        if (_project is null) return;
        var previous = _project.WorkFolder;
        _project.WorkFolder = null; _project.UpdatedAtUtc = DateTimeOffset.UtcNow;
        var result = await _projects.SaveAsync(_project);
        if (result.Succeeded) _folders.Clear(previous); else _project.WorkFolder = previous;
        await ApplyMutationAsync(result);
    }
    private async void OnArchive(object sender, RoutedEventArgs e)
    {
        if (_project is null || !await ConfirmAsync(_project.IsArchived ? "Project_Restore_Title" : "Project_Archive_Title", _project.IsArchived ? "Project_Restore_Message" : "Project_Archive_Message")) return;
        var result = await _projects.ArchiveAsync(_project.Id, !_project.IsArchived); if (result.Succeeded) { _confirmedNavigation = true; Frame.Navigate(_project.IsArchived ? typeof(HomePage) : typeof(ProjectArchivePage)); } else ShowError("Project_Error_SaveFailed");
    }
    private void OnBack(object sender, RoutedEventArgs e) { _confirmedNavigation = true; Frame.Navigate(typeof(HomePage)); }
    private void OnFieldChanged(object sender, TextChangedEventArgs e) { if (!_applying) _dirty = true; }
    private void OnTypeChanged(object sender, SelectionChangedEventArgs e) { if (TypeBox.SelectedItem is ProjectTypeOption option) CustomTypeBox.Visibility = option.Code == ProjectTypeCodes.Other ? Visibility.Visible : Visibility.Collapsed; if (!_applying) _dirty = true; }

    private async Task ApplyMutationAsync(ProjectSaveResult result) { if (!result.Succeeded) { ShowValidation(result.ValidationErrors); return; } _project = result.Project; ShowSuccess("Project_Status_Saved"); ApplyProject(); await Task.CompletedTask; }
    private async Task<bool> ConfirmAsync(string titleKey, string messageKey) { var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = _localization.GetString(titleKey), Content = _localization.GetString(messageKey), PrimaryButtonText = _localization.GetString("Action_Confirm"), CloseButtonText = _localization.GetString("Action_Cancel"), DefaultButton = ContentDialogButton.Close }; return await dialog.ShowAsync() == ContentDialogResult.Primary; }
    private void ShowValidation(IReadOnlyList<string>? errors) { StatusBar.Severity = InfoBarSeverity.Error; StatusBar.Message = errors is null ? _localization.GetString("Project_Error_SaveFailed") : string.Join(Environment.NewLine, errors.Select(error => _localization.GetString($"ProjectValidation_{error}"))); StatusBar.IsOpen = true; }
    private void ShowError(string key) { StatusBar.Severity = InfoBarSeverity.Error; StatusBar.Message = _localization.GetString(key); StatusBar.IsOpen = true; }
    private void ShowSuccess(string key) { StatusBar.Severity = InfoBarSeverity.Success; StatusBar.Message = _localization.GetString(key); StatusBar.IsOpen = true; }
    private static string Format(decimal? value) => value?.ToString("0.##", CultureInfo.CurrentCulture) ?? "—";
    private static bool TryParseCoordinate(string text, out decimal? value) { value = null; if (string.IsNullOrWhiteSpace(text)) return true; if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out var parsed) || decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out parsed)) { value = parsed; return true; } return false; }
    private sealed record ProjectTypeOption(string Code, string Name);
}
