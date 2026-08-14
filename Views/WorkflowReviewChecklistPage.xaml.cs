using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UrbanPlanToolbox.Controls;
using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Models.Tools;
using UrbanPlanToolbox.Models.Interaction;
using UrbanPlanToolbox.Services;

namespace UrbanPlanToolbox.Views;

public sealed partial class WorkflowReviewChecklistPage : Page
{
    private readonly ILocalizationService _localization = LocalizationService.Default;
    private readonly WorkflowReviewChecklistService _storage = new(AppDataPathProvider.Default);
    private List<WorkflowReviewChecklistDocument> _documents = [];
    private WorkflowReviewChecklistDocument? _current;
    private WorkflowReviewChecklistDocument? _baseline;
    private bool _loading;
    private bool _dirty;

    public WorkflowReviewChecklistPage()
    {
        InitializeComponent();
        TitleText.Text = T("Tool_WorkflowReviewChecklist_Name");
        DescriptionText.Text = T("Tool_WorkflowReviewChecklist_Description");
        NameBox.Header = T("Workflow_Name"); DescriptionBox.Header = T("Workflow_Description"); UsageBox.Header = T("Workflow_Usage");
        NewButton.Content = T("Workflow_New"); AddSectionButton.Content = T("Workflow_AddSection");
        SaveButton.Content = T("Workflow_Save"); ResetButton.Content = T("Workflow_Reset"); CopyButton.Content = T("Workflow_Copy"); DeleteButton.Content = T("Workflow_Delete");
        UsageBox.ItemsSource = Enum.GetValues<WorkflowChecklistUsageType>().Select(value => new UsageChoice(value, UsageName(value))).ToArray();
        Loaded += async (_, _) => await LoadAsync();
    }

    private string T(string key) => _localization.GetString(key);
    private string UsageName(WorkflowChecklistUsageType value) => T(value switch { WorkflowChecklistUsageType.Design => "Workflow_Design", WorkflowChecklistUsageType.Research => "Workflow_Research", _ => "Workflow_General" });

    private async Task LoadAsync()
    {
        _loading = true;
        var result = await _storage.ReadAsync();
        _documents = result.HasValue ? result.Value! : [];
        _loading = false;
        RenderList();
        if (_documents.Count > 0) ChecklistList.SelectedIndex = 0; else RenderCurrent(null);
    }

    private void RenderList()
    {
        ChecklistList.Items.Clear();
        foreach (var document in _documents.OrderByDescending(item => item.UpdatedAt))
        {
            var text = new TextBlock { Text = document.Name, TextWrapping = TextWrapping.Wrap, Padding = new Thickness(8) };
            text.Tag = document; ChecklistList.Items.Add(text);
        }
        ListStatus.Text = _documents.Count == 0 ? T("Workflow_Empty") : string.Empty;
    }

    private void OnChecklistSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ChecklistList.SelectedItem is TextBlock { Tag: WorkflowReviewChecklistDocument document }) RenderCurrent(document);
    }

    private void RenderCurrent(WorkflowReviewChecklistDocument? source)
    {
        _loading = true; _current = source is null ? null : WorkflowReviewChecklistService.Clone(source); _baseline = source is null ? null : WorkflowReviewChecklistService.Clone(source); _dirty = false;
        NameBox.Text = _current?.Name ?? string.Empty; DescriptionBox.Text = _current?.Description ?? string.Empty;
        UsageBox.SelectedItem = _current is null ? null : UsageBox.Items.OfType<UsageChoice>().FirstOrDefault(item => item.Value == _current.UsageType);
        RenderDetails(); _loading = false;
    }

    private void RenderDetails()
    {
        SectionsPanel.Children.Clear();
        if (_current is null) { StatisticsText.Text = T("Workflow_Empty"); return; }
        var stats = WorkflowReviewChecklistService.GetStatistics(_current);
        StatisticsText.Text = string.Format(T("Workflow_Statistics"), stats.Total, stats.Pending, stats.Passed, stats.NeedsRevision, stats.NotApplicable, stats.CompletionRate.ToString("0"));
        foreach (var section in _current.Sections.OrderBy(item => item.SortOrder))
        {
            var panel = new StackPanel { Spacing = 6 };
            var header = new Grid(); header.ColumnDefinitions.Add(new ColumnDefinition()); header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var title = new TextBlock { Text = section.Title, Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"], TextWrapping = TextWrapping.Wrap };
            var add = new Button { Content = T("Workflow_AddItem"), Tag = section }; add.Click += OnAddItemClick; Grid.SetColumn(add, 1); header.Children.Add(title); header.Children.Add(add); panel.Children.Add(header);
            if (!string.IsNullOrWhiteSpace(section.Description)) panel.Children.Add(new TextBlock { Text = section.Description, TextWrapping = TextWrapping.Wrap });
            foreach (var item in section.Items.OrderBy(value => value.SortOrder)) panel.Children.Add(CreateItemEditor(section, item));
            SectionsPanel.Children.Add(new Border { Padding = new Thickness(12), BorderThickness = new Thickness(1), BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"], Child = panel });
        }
    }

    private UIElement CreateItemEditor(WorkflowChecklistSection section, WorkflowChecklistItem item)
    {
        var panel = new StackPanel { Spacing = 4 };
        var title = new TextBlock { Text = (item.IsCritical ? "★ " : "") + item.Title, TextWrapping = TextWrapping.Wrap };
        var row = new Grid(); row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var status = new ComboBox { ItemsSource = Enum.GetValues<WorkflowChecklistItemStatus>().Select(value => new StatusChoice(value, StatusName(value))).ToArray(), DisplayMemberPath = nameof(StatusChoice.Display), Tag = item, HorizontalAlignment = HorizontalAlignment.Stretch };
        TransientComboBoxTheme.ApplyTo(status);
        status.SelectedItem = status.Items.OfType<StatusChoice>().First(value => value.Value == item.Status); status.SelectionChanged += OnItemStatusChanged;
        var remove = new Button { Content = "×", Tag = (section, item) }; remove.Click += OnRemoveItemClick; Grid.SetColumn(remove, 1); row.Children.Add(status); row.Children.Add(remove);
        panel.Children.Add(title); if (!string.IsNullOrWhiteSpace(item.Description)) panel.Children.Add(new TextBlock { Text = item.Description, TextWrapping = TextWrapping.Wrap }); panel.Children.Add(row);
        var note = new TextBox { Header = T("Workflow_Note"), Text = item.Note ?? string.Empty, Tag = item, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true }; note.TextChanged += OnItemNoteChanged; panel.Children.Add(note);
        return panel;
    }

    private string StatusName(WorkflowChecklistItemStatus value) => T(value switch { WorkflowChecklistItemStatus.Passed => "Workflow_Passed", WorkflowChecklistItemStatus.NeedsRevision => "Workflow_NeedsRevision", WorkflowChecklistItemStatus.NotApplicable => "Workflow_NotApplicable", _ => "Workflow_Pending" });
    private void OnDraftChanged(object sender, TextChangedEventArgs e) { if (!_loading && _current is not null) _dirty = true; }
    private void OnUsageChanged(object sender, SelectionChangedEventArgs e) { if (!_loading && _current is not null) _dirty = true; }
    private void OnItemStatusChanged(object sender, SelectionChangedEventArgs e) { if (sender is ComboBox { Tag: WorkflowChecklistItem item, SelectedItem: StatusChoice choice }) { item.Status = choice.Value; _dirty = true; RenderDetails(); } }
    private void OnItemNoteChanged(object sender, TextChangedEventArgs e) { if (sender is TextBox { Tag: WorkflowChecklistItem item }) { item.Note = ((TextBox)sender).Text; if (!_loading) _dirty = true; } }

    private async void OnNewClick(object sender, RoutedEventArgs e)
    {
        var name = await PromptAsync(T("Workflow_New"), T("Workflow_Name"), T("Workflow_Create")); if (string.IsNullOrWhiteSpace(name)) return;
        var now = DateTimeOffset.UtcNow; var document = new WorkflowReviewChecklistDocument { Name = name.Trim(), CreatedAt = now, UpdatedAt = now };
        _documents.Add(document); RenderList(); ChecklistList.SelectedIndex = ChecklistList.Items.Count - 1;
    }

    private async void OnAddSectionClick(object sender, RoutedEventArgs e) { if (_current is null) return; var title = await PromptAsync(T("Workflow_AddSection"), T("Workflow_SectionTitle"), T("Workflow_Create")); if (string.IsNullOrWhiteSpace(title)) return; _current.Sections.Add(new WorkflowChecklistSection { Title = title.Trim(), SortOrder = _current.Sections.Count }); _dirty = true; RenderDetails(); }
    private async void OnAddItemClick(object sender, RoutedEventArgs e) { if (sender is not Button { Tag: WorkflowChecklistSection section } || _current is null) return; var title = await PromptAsync(T("Workflow_AddItem"), T("Workflow_ItemTitle"), T("Workflow_Create")); if (string.IsNullOrWhiteSpace(title)) return; section.Items.Add(new WorkflowChecklistItem { Title = title.Trim(), SortOrder = section.Items.Count }); _dirty = true; RenderDetails(); }
    private void OnRemoveItemClick(object sender, RoutedEventArgs e) { if (sender is Button { Tag: ValueTuple<WorkflowChecklistSection, WorkflowChecklistItem> pair }) { pair.Item1.Items.Remove(pair.Item2); _dirty = true; RenderDetails(); } }

    private async void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (_current is null) return; _current.Name = NameBox.Text.Trim(); _current.Description = string.IsNullOrWhiteSpace(DescriptionBox.Text) ? null : DescriptionBox.Text.Trim(); if (UsageBox.SelectedItem is UsageChoice usage) _current.UsageType = usage.Value;
        WorkflowReviewChecklistService.NormalizeSortOrders(_current); _current.UpdatedAt = DateTimeOffset.UtcNow;
        var result = await _storage.SaveAsync(_documents.Select(item => item.ChecklistId == _current.ChecklistId ? _current : item).ToArray());
        if (!result.Succeeded) { AppNotificationService.Default.Notify(new(AppNotificationKind.Error, T("Interaction_ErrorTitle"), T("Workflow_SaveFailed"))); return; }
        var index = _documents.FindIndex(item => item.ChecklistId == _current.ChecklistId); _documents[index] = WorkflowReviewChecklistService.Clone(_current); _dirty = false; RenderList(); ChecklistList.SelectedIndex = index; AppNotificationService.Default.Notify(new(AppNotificationKind.Success, T("Interaction_SuccessTitle"), T("Workflow_Saved")));
    }
    private void OnResetClick(object sender, RoutedEventArgs e) { if (!_dirty || _baseline is null) return; RenderCurrent(_baseline); }
    private async void OnCopyClick(object sender, RoutedEventArgs e) { if (_current is null) return; var copy = WorkflowReviewChecklistService.Clone(_current); copy = new WorkflowReviewChecklistDocument { Name = copy.Name + " (2)", Description = copy.Description, UsageType = copy.UsageType, Sections = copy.Sections.Select(section => new WorkflowChecklistSection { Title = section.Title, Description = section.Description, SortOrder = section.SortOrder, Items = section.Items.Select(item => new WorkflowChecklistItem { Title = item.Title, Description = item.Description, IsCritical = item.IsCritical, SortOrder = item.SortOrder }).ToList() }).ToList() }; _documents.Add(copy); RenderList(); ChecklistList.SelectedIndex = ChecklistList.Items.Count - 1; await Task.CompletedTask; }
    private async void OnDeleteClick(object sender, RoutedEventArgs e) { if (_current is null) return; var result = await ConfirmAsync(T("Workflow_Delete"), T("Workflow_DeleteMessage")); if (result != ContentDialogResult.Primary) return; _documents.RemoveAll(item => item.ChecklistId == _current.ChecklistId); await _storage.SaveAsync(_documents); RenderList(); if (_documents.Count > 0) ChecklistList.SelectedIndex = 0; else RenderCurrent(null); }
    private async Task<string?> PromptAsync(string title, string header, string action) { var box = new TextBox { Header = header }; var dialog = new ContentDialog { Title = title, Content = box, PrimaryButtonText = action, CloseButtonText = T("Action_Cancel"), DefaultButton = ContentDialogButton.Primary, XamlRoot = XamlRoot }; return await AppDialogService.Default.ShowAsync(dialog) == ContentDialogResult.Primary ? box.Text : null; }
    private Task<ContentDialogResult> ConfirmAsync(string title, string message) => AppDialogService.Default.ShowAsync(new ContentDialog { Title = title, Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap }, PrimaryButtonText = T("Workflow_Delete"), CloseButtonText = T("Action_Cancel"), DefaultButton = ContentDialogButton.Close, XamlRoot = XamlRoot });

    private sealed record UsageChoice(WorkflowChecklistUsageType Value, string Display);
    private sealed record StatusChoice(WorkflowChecklistItemStatus Value, string Display);
}
