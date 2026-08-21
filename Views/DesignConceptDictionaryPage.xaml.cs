using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Services;

namespace UrbanPlanToolbox.Views;

public sealed partial class DesignConceptDictionaryPage : Page
{
    private const string PackId = ReferenceDataPackIds.DesignConcepts;
    private readonly ILocalizationService _localization = LocalizationService.Default;
    private ReferenceDataPackContent? _pack;
    private DesignConceptsPackDocument? _document;
    private DesignConceptRecord? _selected;
    private DesignConceptLocalization _conceptLocalization = DesignConceptLocalization.Empty;
    private bool _loading;
    private bool _busy;

    public DesignConceptDictionaryPage()
    {
        InitializeComponent();
        ApplyText();
        DetailPanel.Visibility = Visibility.Collapsed;
        Loaded += OnLoaded;
        Root.SizeChanged += OnRootSizeChanged;
    }

    private void ApplyText()
    {
        BackButton.Content = ReferenceLibraryText.Get("BackDesign");
        TitleText.Text = _localization.GetString("Tool_DesignConceptDictionary_Name");
        DescriptionText.Text = _localization.GetString("Tool_DesignConceptDictionary_Description");
        HeaderCheckButton.Content = ReferenceLibraryText.Get("CheckDataUpdate");
        CurrentSourceLabel.Text = ReferenceLibraryText.Get("CurrentSource");
        CheckButton.Content = ReferenceLibraryText.Get("CheckUpdate");
        ManageButton.Content = ReferenceLibraryText.Get("ManageSource");
        SearchBox.PlaceholderText = ReferenceLibraryText.Get("ConceptSearch");
        ResetButton.Content = ReferenceLibraryText.Get("ResetFilters");
        ListHeader.Text = ReferenceLibraryText.Get("ConceptEntries");
        DefinitionHeader.Text = ReferenceLibraryText.Get("Definition");
        ProjectTypesHeader.Text = ReferenceLibraryText.Get("ProjectTypes");
        TagsHeader.Text = ReferenceLibraryText.Get("Tags");
        CaseHeader.Text = ReferenceLibraryText.Get("CaseNote");
        ViewSourceButton.Content = ReferenceLibraryText.Get("ViewSource");
        CopyButton.Content = ReferenceLibraryText.Get("Copy");
        CloudVersionText.Text = ReferenceLibraryText.Get("CloudUnavailable");
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await ReloadAsync();
        CloudVersionText.Text = await ReferenceDataPackPageCoordinator.GetCloudVersionTextAsync(PackId);
    }

    private async Task ReloadAsync()
    {
        _loading = true;
        try
        {
            _pack = await ReferenceDataPackService.Default.LoadActiveAsync(PackId);
            _document = _pack is null ? null : ReferenceDataPackService.ParseDesignConcepts(_pack.DataJson);
            _conceptLocalization = _pack is null ? DesignConceptLocalization.Empty : DesignConceptLocalization.Parse(_pack.DataJson);
            _selected = null;
            RenderSource();
            RebuildFilters();
            RefreshResults();
        }
        catch (Exception exception)
        {
            AppLogger.Default.Error(nameof(DesignConceptDictionaryPage), "design_concepts_pack_load_failed", exception);
            _pack = null;
            _document = null;
            _selected = null;
            _conceptLocalization = DesignConceptLocalization.Empty;
            RenderSource();
            RebuildFilters();
            RefreshResults();
            ShowStatus(ReferenceLibraryText.Get("PackFailed", exception.Message), InfoBarSeverity.Error);
        }
        finally { _loading = false; }
    }

    private void RenderSource()
    {
        if (_pack is null || _document is null)
        {
            SourceNameText.Text = ReferenceLibraryText.Get("NoDataPack");
            SourceMetaText.Text = ReferenceLibraryText.Get("NoDataPackHint");
            return;
        }
        SourceNameText.Text = ReferenceDataPackService.GetLocalized(_pack.Manifest.DisplayName, _localization.CurrentLanguage);
        SourceMetaText.Text = ReferenceLibraryText.Get("PackMeta", _pack.State.ArchiveFileName, _document.Entries.Count, _pack.Manifest.SchemaVersion);
    }

    private void RebuildFilters()
    {
        var projectType = (ProjectTypeBox.SelectedItem as Choice)?.Value;
        var tag = (TagBox.SelectedItem as Choice)?.Value;
        var sort = (SortBox.SelectedItem as Choice)?.Value;
        var entries = _document?.Entries ?? [];
        var language = _localization.CurrentLanguage;
        _loading = true;

        var projectChoices = entries
            .SelectMany(entry => entry.ProjectTypes)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .Select(value => new Choice(value, _conceptLocalization.ProjectType(value, language)))
            .Where(choice => !string.IsNullOrWhiteSpace(choice.Display))
            .OrderBy(choice => choice.Display, StringComparer.CurrentCultureIgnoreCase);
        ProjectTypeBox.ItemsSource = new[] { new Choice(string.Empty, ReferenceLibraryText.Get("AllProjectTypes")) }.Concat(projectChoices).ToArray();

        var tagChoices = entries
            .SelectMany(entry => entry.Tags)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .Select(value => new Choice(value, _conceptLocalization.Tag(value, language)))
            .Where(choice => !string.IsNullOrWhiteSpace(choice.Display))
            .OrderBy(choice => choice.Display, StringComparer.CurrentCultureIgnoreCase);
        TagBox.ItemsSource = new[] { new Choice(string.Empty, ReferenceLibraryText.Get("AllTags")) }.Concat(tagChoices).ToArray();

        SortBox.ItemsSource = new[] { new Choice("recent", ReferenceLibraryText.Get("Recent")), new Choice("name", ReferenceLibraryText.Get("NameSort")) };
        SelectChoice(ProjectTypeBox, projectType);
        SelectChoice(TagBox, tag);
        SelectChoice(SortBox, string.IsNullOrWhiteSpace(sort) ? "recent" : sort);
        _loading = false;
    }

    private static void SelectChoice(ComboBox box, string? value)
    {
        var choices = box.ItemsSource?.Cast<Choice>().ToArray() ?? [];
        box.SelectedItem = choices.FirstOrDefault(choice => string.Equals(choice.Value, value, StringComparison.Ordinal)) ?? choices.FirstOrDefault();
    }

    private void RefreshResults()
    {
        var entries = _document?.Entries ?? [];
        var query = SearchBox.Text.Trim();
        var projectType = (ProjectTypeBox.SelectedItem as Choice)?.Value;
        var tag = (TagBox.SelectedItem as Choice)?.Value;
        var sort = (SortBox.SelectedItem as Choice)?.Value ?? "recent";
        var language = _localization.CurrentLanguage;
        IEnumerable<DesignConceptRecord> filtered = entries.Where(entry =>
            (string.IsNullOrWhiteSpace(projectType) || entry.ProjectTypes.Contains(projectType, StringComparer.Ordinal)) &&
            (string.IsNullOrWhiteSpace(tag) || entry.Tags.Contains(tag, StringComparer.Ordinal)) &&
            (query.Length == 0 || BuildSearchText(entry).Contains(query, StringComparison.OrdinalIgnoreCase)));
        filtered = sort == "name"
            ? filtered.OrderBy(entry => DisplayTitle(entry), StringComparer.CurrentCultureIgnoreCase)
            : filtered.OrderByDescending(entry => ParseReviewed(entry.LastReviewed)).ThenBy(entry => entry.Id);
        var results = filtered.Select(entry => new ConceptListItem(
            entry,
            DisplayTitle(entry),
            DisplayDefinition(entry),
            string.Join(" · ", new[]
            {
                _conceptLocalization.Category(entry.Category, language),
                entry.ProjectTypes.Select(value => _conceptLocalization.ProjectType(value, language)).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)),
                ReviewStatusLabel(entry.ReviewStatus)
            }.Where(value => !string.IsNullOrWhiteSpace(value))),
            string.Join(" / ", _conceptLocalization.Tags(entry.Tags.Take(3), language)))).ToArray();
        ConceptsList.ItemsSource = results;
        CountText.Text = ReferenceLibraryText.Get("ConceptCount", entries.Count, results.Length);
        ListCountBadge.Text = results.Length.ToString();
        EmptyText.Text = results.Length == 0 ? (_pack is null ? ReferenceLibraryText.Get("NoDataPackHint") : ReferenceLibraryText.Get("NoResults")) : string.Empty;
        if (results.Length > 0)
        {
            var target = _selected is null ? results[0] : results.FirstOrDefault(item => item.Entry.StableId == _selected.StableId) ?? results[0];
            ConceptsList.SelectedItem = target;
        }
        else
        {
            _selected = null;
            DetailPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void OnFilterChanged(object sender, TextChangedEventArgs e) { if (!_loading) RefreshResults(); }
    private void OnSelectionFilterChanged(object sender, SelectionChangedEventArgs e) { if (!_loading) RefreshResults(); }
    private void OnResetFiltersClick(object sender, RoutedEventArgs e)
    {
        _loading = true;
        SearchBox.Text = string.Empty;
        ProjectTypeBox.SelectedIndex = 0;
        TagBox.SelectedIndex = 0;
        SortBox.SelectedIndex = 0;
        _loading = false;
        RefreshResults();
    }

    private void OnConceptSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ConceptsList.SelectedItem is not ConceptListItem item) return;
        _selected = item.Entry;
        RenderDetail(item.Entry);
    }

    private void RenderDetail(DesignConceptRecord entry)
    {
        var language = _localization.CurrentLanguage;
        DetailTitle.Text = DisplayTitle(entry);
        CategoryBadge.Text = _conceptLocalization.Category(entry.Category, language);
        DetailMeta.Text = $"{ReviewStatusLabel(entry.ReviewStatus)} · {entry.LastReviewed} · {entry.StableId}";
        DetailDefinition.Text = DisplayDefinition(entry);
        ProjectTypesList.ItemsSource = _conceptLocalization.ProjectTypes(entry.ProjectTypes, language);
        TagsList.ItemsSource = _conceptLocalization.Tags(entry.Tags, language);
        CaseText.Text = DisplayCaseNote(entry);
        ViewSourceButton.Visibility = ResolveSources(entry).Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        DetailPanel.Visibility = Visibility.Visible;
    }

    private IReadOnlyList<DesignConceptSource> ResolveSources(DesignConceptRecord entry)
    {
        if (_document is null) return [];
        var ids = entry.SourceIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return _document.Sources.Where(source => ids.Contains(source.Id)).ToArray();
    }

    private async void OnViewSourceClick(object sender, RoutedEventArgs e)
    {
        if (_selected is null) return;
        var sources = ResolveSources(_selected);
        if (sources.Count == 0) return;
        var body = string.Join("\n\n", sources.Select(source =>
        {
            var name = ReferenceDataPackService.GetLocalized(source.Name, _localization.CurrentLanguage);
            var note = ReferenceDataPackService.GetLocalized(source.Note, _localization.CurrentLanguage);
            return string.Join("\n", new[] { name, source.Type, note }.Where(value => !string.IsNullOrWhiteSpace(value)));
        }));
        await AppDialogService.Default.ShowAsync(new ContentDialog { XamlRoot = XamlRoot, Title = ReferenceLibraryText.Get("Sources"), Content = body, CloseButtonText = ReferenceLibraryText.Get("Close") });
    }

    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        if (_selected is null) return;
        var sourceNames = ResolveSources(_selected).Select(source => ReferenceDataPackService.GetLocalized(source.Name, _localization.CurrentLanguage));
        var text = string.Join("\n", new[] { DisplayTitle(_selected), _selected.StableId, _pack?.Manifest.Version, string.Join("; ", sourceNames) }.Where(value => !string.IsNullOrWhiteSpace(value)));
        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);
        ShowStatus(ReferenceLibraryText.Get("Copied"), InfoBarSeverity.Success);
    }

    private async void OnCheckUpdateClick(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        SetBusy(true);
        try
        {
            if (await ReferenceDataPackPageCoordinator.CheckAndInstallUpdateAsync(Root, PackId, StatusBar)) await ReloadAsync();
            CloudVersionText.Text = await ReferenceDataPackPageCoordinator.GetCloudVersionTextAsync(PackId);
        }
        finally { SetBusy(false); }
    }

    private async void OnManageSourceClick(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        SetBusy(true);
        try { if (await ReferenceDataPackPageCoordinator.ManageAsync(Root, PackId, StatusBar)) await ReloadAsync(); }
        finally { SetBusy(false); }
    }

    private void OnBackClick(object sender, RoutedEventArgs e) { if (Frame.CanGoBack) Frame.GoBack(); }

    private void OnRootSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var narrow = e.NewSize.Width > 0 && e.NewSize.Width < 900;
        if (narrow)
        {
            ContentGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
            ContentGrid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
            ContentGrid.RowDefinitions[0].Height = new GridLength(Math.Clamp(Root.ActualHeight * 0.38, 260, 360));
            ContentGrid.RowDefinitions[1].Height = new GridLength(1, GridUnitType.Star);
            Grid.SetRow(ListPanel, 0); Grid.SetColumn(ListPanel, 0); Grid.SetColumnSpan(ListPanel, 2);
            Grid.SetRow(DetailPanel, 1); Grid.SetColumn(DetailPanel, 0); Grid.SetColumnSpan(DetailPanel, 2);
        }
        else
        {
            ContentGrid.RowDefinitions[0].Height = new GridLength(1, GridUnitType.Star);
            ContentGrid.RowDefinitions[1].Height = new GridLength(0);
            ContentGrid.ColumnDefinitions[0].Width = new GridLength(0.85, GridUnitType.Star);
            ContentGrid.ColumnDefinitions[1].Width = new GridLength(1.15, GridUnitType.Star);
            Grid.SetRow(ListPanel, 0); Grid.SetColumn(ListPanel, 0); Grid.SetColumnSpan(ListPanel, 1);
            Grid.SetRow(DetailPanel, 0); Grid.SetColumn(DetailPanel, 1); Grid.SetColumnSpan(DetailPanel, 1);
        }
    }

    private string DisplayTitle(DesignConceptRecord entry) => ReferenceDataPackService.GetLocalized(entry.Title, _localization.CurrentLanguage);
    private string DisplayDefinition(DesignConceptRecord entry) => ReferenceDataPackService.GetLocalized(entry.Definition, _localization.CurrentLanguage);
    private string DisplayCaseNote(DesignConceptRecord entry)
    {
        var language = _localization.CurrentLanguage;
        var locale = language.StartsWith("ja", StringComparison.OrdinalIgnoreCase) ? "ja-JP" : language.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "en-US" : "zh-CN";
        return entry.CaseNote.TryGetValue(locale, out var value) && !string.IsNullOrWhiteSpace(value) ? value : string.Empty;
    }

    private string BuildSearchText(DesignConceptRecord entry)
    {
        var sourceText = string.Join('\n', ResolveSources(entry).SelectMany(source => source.Name.Values.Concat(source.Note.Values)));
        var labelText = string.Join('\n', _conceptLocalization.SearchTerms(entry.Category, entry.ProjectTypes, entry.Tags));
        return string.Join('\n', entry.Title.Values.Concat(entry.Aliases).Concat(entry.Definition.Values).Concat(entry.ProjectTypes).Concat(entry.Tags).Concat(entry.CaseNote.Values).Append(entry.Category).Append(labelText).Append(sourceText));
    }

    private string ReviewStatusLabel(string status) => status switch { "verified" => ReferenceLibraryText.Get("Verified"), "reviewed" => ReferenceLibraryText.Get("Reviewed"), _ => ReferenceLibraryText.Get("Seed") };
    private static DateTimeOffset ParseReviewed(string value) => DateTimeOffset.TryParse(value, out var parsed) ? parsed : DateTimeOffset.MinValue;
    private void SetBusy(bool value) { _busy = value; HeaderCheckButton.IsEnabled = CheckButton.IsEnabled = ManageButton.IsEnabled = !value; }
    private void ShowStatus(string message, InfoBarSeverity severity) { StatusBar.Message = message; StatusBar.Severity = severity; StatusBar.IsOpen = true; }

    private sealed record Choice(string Value, string Display);
    private sealed record ConceptListItem(DesignConceptRecord Entry, string Name, string Definition, string Metadata, string TagsText);
}
