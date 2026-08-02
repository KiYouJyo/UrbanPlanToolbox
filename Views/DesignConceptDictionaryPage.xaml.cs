using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using System.Diagnostics;
using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Services;

namespace UrbanPlanToolbox.Views;

public sealed partial class DesignConceptDictionaryPage : Page
{
    private readonly ILocalizationService _localization = LocalizationService.Default;
    private readonly DesignConceptDictionaryService _storage = new(AppDataPathProvider.Default);
    private DesignConceptDictionaryDocument _document = new();
    private DesignConcept? _editing;
    private DesignConcept? _savedEditing;
    private DesignConceptEditSnapshot? _baseline;
    private bool _isNew;
    private bool _loading;
    private bool _dirty;

    public DesignConceptDictionaryPage()
    {
        InitializeComponent();
        TitleText.Text = T("Tool_DesignConceptDictionary_Name");
        DescriptionText.Text = T("Tool_DesignConceptDictionary_Description");
        NewButton.Content = T("Concept_New");
        SearchBox.PlaceholderText = T("Concept_SearchPlaceholder");
        ProjectTypeFilter.PlaceholderText = T("Concept_ProjectTypeFilter");
        TagFilter.PlaceholderText = T("Concept_TagFilter");
        NameBox.Header = T("Concept_Name");
        DefinitionBox.Header = T("Concept_Definition");
        SourceBox.Header = T("Concept_Source");
        NotesBox.Header = T("Concept_Notes");
        ProjectTypesEditor.Label = T("Concept_ProjectTypes");
        ProjectTypesEditor.AddButtonText = T("Concept_Add");
        ProjectTypesEditor.PlaceholderText = T("Concept_ProjectTypePlaceholder");
        TagsEditor.Label = T("Concept_Tags");
        TagsEditor.AddButtonText = T("Concept_Add");
        TagsEditor.PlaceholderText = T("Concept_TagPlaceholder");
        AutomationProperties.SetName(ProjectTypeFilter, T("Concept_ProjectTypeFilter"));
        AutomationProperties.SetName(TagFilter, T("Concept_TagFilter"));
        AutomationProperties.SetName(SortBox, T("Concept_SortLastModified"));
        BackButton.Content = T("Action_Back");
        SaveButton.Content = T("Concept_Save");
        ResetButton.Content = T("Concept_Reset");
        CopyButton.Content = T("Concept_Copy");
        DeleteButton.Content = T("Concept_Delete");
        SortBox.ItemsSource = new[] { new FilterChoice("last", T("Concept_SortLastModified")), new FilterChoice("created", T("Concept_SortCreated")), new FilterChoice("name", T("Concept_SortName")) };
        SortBox.SelectedIndex = 0;
        Loaded += async (_, _) => await LoadAsync();
    }

    private string T(string key) => _localization.GetString(key);
    private string T(string key, params object[] args) => string.Format(_localization.GetString(key), args);

    private async Task LoadAsync()
    {
        var result = await _storage.ReadAsync();
        if (!result.HasValue)
        {
            ShowStatus(ListStatus, T("Concept_LoadFailed"));
            return;
        }
        _document = result.Value!;
        RefreshFilters();
        RenderCards();
    }

    private void RefreshFilters()
    {
        var selectedProjectType = (ProjectTypeFilter.SelectedItem as FilterChoice)?.Value;
        var selectedTag = (TagFilter.SelectedItem as FilterChoice)?.Value;
        _loading = true;
        var projectChoices = new[] { new FilterChoice(string.Empty, T("Concept_AllProjectTypes")) }.Concat(DesignConceptDictionaryService.GetProjectTypes(_document).Select(value => new FilterChoice(value, value))).ToArray();
        var tagChoices = new[] { new FilterChoice(string.Empty, T("Concept_AllTags")) }.Concat(DesignConceptDictionaryService.GetTags(_document).Select(value => new FilterChoice(value, value))).ToArray();
        ProjectTypeFilter.ItemsSource = projectChoices;
        TagFilter.ItemsSource = tagChoices;
        ProjectTypeFilter.SelectedItem = projectChoices.FirstOrDefault(choice => string.Equals(choice.Value, selectedProjectType, StringComparison.OrdinalIgnoreCase)) ?? projectChoices[0];
        TagFilter.SelectedItem = tagChoices.FirstOrDefault(choice => string.Equals(choice.Value, selectedTag, StringComparison.OrdinalIgnoreCase)) ?? tagChoices[0];
        _loading = false;
    }

    private void RenderCards()
    {
        var sort = SortBox.SelectedItem is FilterChoice { Value: "created" } ? DesignConceptSort.Created : SortBox.SelectedItem is FilterChoice { Value: "name" } ? DesignConceptSort.Name : DesignConceptSort.LastModified;
        var projectType = (ProjectTypeFilter.SelectedItem as FilterChoice)?.Value;
        var tag = (TagFilter.SelectedItem as FilterChoice)?.Value;
        var concepts = DesignConceptDictionaryService.Search(_document, SearchBox.Text, projectType, tag, sort);
        ConceptsList.ItemsSource = concepts.Select(concept => new ConceptCard(concept, string.Join(" • ", concept.ApplicableProjectTypes), string.Join(" • ", concept.Tags), T("Concept_LastModified", concept.UpdatedAt.ToLocalTime().ToString("g")), concept.Name)).ToArray();
        CountText.Text = T("Concept_Count", concepts.Count, _document.Concepts.Count);
        ListStatus.Text = concepts.Count == 0 ? T("Concept_Empty") : string.Empty;
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs args) { if (!_loading) RenderCards(); }
    private void OnSelectionFilterChanged(object sender, SelectionChangedEventArgs args) { if (!_loading) RenderCards(); }

    private void OnNewClick(object sender, RoutedEventArgs e) => BeginEdit(null);
    private void OnConceptClick(object sender, RoutedEventArgs e) { if (sender is Button { Tag: ConceptCard card }) BeginEdit(_document.Concepts.FirstOrDefault(concept => concept.ConceptId == card.ConceptId)); }

    private void BeginEdit(DesignConcept? source)
    {
        _isNew = source is null;
        _savedEditing = source is null ? new DesignConcept { ConceptId = Guid.NewGuid(), CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow } : DesignConceptDictionaryService.Clone(source);
        _editing = DesignConceptDictionaryService.Clone(_savedEditing);
        ListPanel.Visibility = Visibility.Collapsed;
        EditorPanel.Visibility = Visibility.Visible;
        NewButton.Visibility = Visibility.Collapsed;
        RenderEditor();
        _dirty = false;
    }

    private void RenderEditor()
    {
        if (_editing is null) return;
        _loading = true;
        EditorTitle.Text = _isNew ? T("Concept_New") : _editing.Name;
        NameBox.Text = _editing.Name;
        DefinitionBox.Text = _editing.Definition;
        ProjectTypesEditor.SetValues(_editing.ApplicableProjectTypes);
        TagsEditor.SetValues(_editing.Tags);
        SourceBox.Text = _editing.SourceOrReference ?? string.Empty;
        NotesBox.Text = _editing.Notes ?? string.Empty;
        TimestampsText.Text = _isNew ? string.Empty : T("Concept_Timestamps", _editing.CreatedAt.ToLocalTime().ToString("g"), _editing.UpdatedAt.ToLocalTime().ToString("g"));
        _loading = false;
        _baseline = DesignConceptDictionaryService.CreateEditSnapshot(_editing);
    }

    private void OnEditorChanged(object sender, TextChangedEventArgs e) { if (!_loading) UpdateDraftAndDirty(); }
    private void OnTagValuesChanged(object? sender, EventArgs e) { if (!_loading) UpdateDraftAndDirty(); }

    private void UpdateDraftAndDirty()
    {
        if (_editing is null) return;
        _editing.Name = NameBox.Text;
        _editing.Definition = DefinitionBox.Text;
        _editing.ApplicableProjectTypes = ProjectTypesEditor.Values.ToList();
        _editing.Tags = TagsEditor.Values.ToList();
        _editing.SourceOrReference = SourceBox.Text;
        _editing.Notes = NotesBox.Text;
        var current = DesignConceptDictionaryService.CreateEditSnapshot(_editing);
        _dirty = _baseline is not null && DesignConceptDictionaryService.HasBusinessChanges(_baseline, current);
        if (_dirty && _baseline is not null) Debug.WriteLine($"DesignConceptDictionary dirty fields: {string.Join(',', DesignConceptDictionaryService.GetChangedFields(_baseline, current))}");
        EditorStatus.IsOpen = false;
    }

    private async void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (_editing is null) return;
        UpdateDraftAndDirty();
        var draft = new DesignConceptDraft { Name = _editing.Name, Definition = _editing.Definition, ApplicableProjectTypes = [.. _editing.ApplicableProjectTypes], Tags = [.. _editing.Tags], SourceOrReference = _editing.SourceOrReference, Notes = _editing.Notes };
        var now = DateTimeOffset.UtcNow;
        var updatedAt = _isNew || _dirty ? now : _editing.UpdatedAt;
        if (!DesignConceptDictionaryService.TryBuildConcept(draft, _editing.ConceptId, _editing.CreatedAt, updatedAt, out var concept, out var error)) { ShowEditorError(error); return; }
        var next = DesignConceptDictionaryService.CloneDocument(_document);
        var index = next.Concepts.FindIndex(item => item.ConceptId == concept.ConceptId);
        if (index < 0) next.Concepts.Add(concept); else next.Concepts[index] = concept;
        var result = await _storage.SaveAsync(next);
        if (!result.Succeeded) { ShowEditorError("Concept_SaveFailed"); return; }
        _document = next;
        _isNew = false;
        _savedEditing = DesignConceptDictionaryService.Clone(concept);
        _editing = DesignConceptDictionaryService.Clone(concept);
        _dirty = false;
        RefreshFilters();
        RenderEditor();
        ShowEditorSuccess(T("Concept_Saved"));
    }

    private async void OnBackClick(object sender, RoutedEventArgs e)
    {
        UpdateDraftAndDirty();
        if (_dirty && await ConfirmDiscardAsync() != ContentDialogResult.Primary) return;
        ReturnToList();
    }

    private void OnResetClick(object sender, RoutedEventArgs e)
    {
        if (_savedEditing is null) return;
        _editing = DesignConceptDictionaryService.Clone(_savedEditing);
        RenderEditor();
        _dirty = false;
    }

    private async void OnCopyClick(object sender, RoutedEventArgs e)
    {
        if (_editing is null) return;
        UpdateDraftAndDirty();
        if (_dirty && await ConfirmDiscardAsync() != ContentDialogResult.Primary) return;
        var copy = DesignConceptDictionaryService.CreateCopy(_editing, T("Concept_CopySuffix"), DateTimeOffset.UtcNow);
        var next = DesignConceptDictionaryService.CloneDocument(_document);
        next.Concepts.Add(copy);
        var result = await _storage.SaveAsync(next);
        if (!result.Succeeded) { ShowEditorError("Concept_SaveFailed"); return; }
        _document = next;
        _isNew = false;
        _savedEditing = DesignConceptDictionaryService.Clone(copy);
        _editing = DesignConceptDictionaryService.Clone(copy);
        RefreshFilters();
        RenderEditor();
        _dirty = false;
        ShowEditorSuccess(T("Concept_Copied"));
    }

    private async void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (_editing is null) return;
        if (_isNew) { ReturnToList(); return; }
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = T("Concept_DeleteTitle"), Content = T("Concept_DeleteMessage", _editing.Name), PrimaryButtonText = T("Concept_Delete"), CloseButtonText = T("Action_Cancel"), DefaultButton = ContentDialogButton.Close };
        if (await AppDialogService.Default.ShowAsync(dialog) != ContentDialogResult.Primary) return;
        var next = DesignConceptDictionaryService.CloneDocument(_document);
        next.Concepts.RemoveAll(concept => concept.ConceptId == _editing.ConceptId);
        var result = await _storage.SaveAsync(next);
        if (!result.Succeeded) { ShowEditorError("Concept_DeleteFailed"); return; }
        _document = next;
        ReturnToList();
    }

    private async Task<ContentDialogResult> ConfirmDiscardAsync() => await AppDialogService.Default.ShowAsync(new ContentDialog { XamlRoot = XamlRoot, Title = T("Concept_UnsavedTitle"), Content = T("Concept_UnsavedMessage"), PrimaryButtonText = T("Concept_Discard"), CloseButtonText = T("Action_Cancel"), DefaultButton = ContentDialogButton.Close });
    private void ReturnToList()
    {
        _editing = null;
        _savedEditing = null;
        _baseline = null;
        _dirty = false;
        EditorPanel.Visibility = Visibility.Collapsed;
        ListPanel.Visibility = Visibility.Visible;
        NewButton.Visibility = Visibility.Visible;
        RefreshFilters();
        RenderCards();
    }
    private void ShowEditorError(string? error)
    {
        var resourceKey = error switch
        {
            "ConceptNameRequired" => "Concept_NameRequired",
            "ConceptDefinitionRequired" => "Concept_DefinitionRequired",
            _ => error is null ? "Concept_SaveFailed" : error
        };
        EditorStatus.Severity = InfoBarSeverity.Error;
        EditorStatus.Message = T(resourceKey);
        EditorStatus.IsOpen = true;
    }
    private void ShowEditorSuccess(string message) { EditorStatus.Severity = InfoBarSeverity.Success; EditorStatus.Message = message; EditorStatus.IsOpen = true; }
    private static void ShowStatus(TextBlock block, string value) => block.Text = value;

    private sealed record FilterChoice(string Value, string Display);
    private sealed record ConceptCard(Guid ConceptId, string Name, string Definition, string ProjectTypesText, string TagsText, string UpdatedText, string AutomationName)
    {
        public ConceptCard(DesignConcept concept, string projectTypesText, string tagsText, string updatedText, string automationName) : this(concept.ConceptId, concept.Name, concept.Definition, projectTypesText, tagsText, updatedText, automationName) { }
    }
}
