using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Text.Json;
using Windows.ApplicationModel.DataTransfer;
using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Services;

namespace UrbanPlanToolbox.Views;

public sealed partial class PlanningTerminologyPage : Page
{
    private const string PackId = ReferenceDataPackIds.PlanningTerminology;
    private readonly ILocalizationService _localization = LocalizationService.Default;
    private ReferenceDataPackContent? _pack;
    private PlanningTerminologyPackDocument? _document;
    private PlanningTerminologyRecord? _selected;
    private string? _selectedSourceUrl;
    private bool _loading;
    private bool _busy;

    public PlanningTerminologyPage()
    {
        InitializeComponent();
        ApplyText();
        DetailPanel.Visibility = Visibility.Collapsed;
        Loaded += OnLoaded;
        Root.SizeChanged += OnRootSizeChanged;
    }

    private void ApplyText()
    {
        BackButton.Content = ReferenceLibraryText.Get("BackResearch");
        TitleText.Text = _localization.GetString("Tool_PlanningTerminology_Name");
        DescriptionText.Text = _localization.GetString("Tool_PlanningTerminology_Description");
        HeaderCheckButton.Content = ReferenceLibraryText.Get("CheckDataUpdate");
        CurrentSourceLabel.Text = ReferenceLibraryText.Get("CurrentSource");
        CheckButton.Content = ReferenceLibraryText.Get("CheckUpdate");
        ManageButton.Content = ReferenceLibraryText.Get("ManageSource");
        SearchBox.PlaceholderText = ReferenceLibraryText.Get("TermSearch");
        ResetButton.Content = ReferenceLibraryText.Get("ResetFilters");
        ListHeader.Text = ReferenceLibraryText.Get("Terms");
        DefinitionHeader.Text = ReferenceLibraryText.Get("Definition");
        ComparisonHeader.Text = ReferenceLibraryText.Get("Comparison");
        RelatedHeader.Text = ReferenceLibraryText.Get("RelatedTerms");
        SourcesHeader.Text = ReferenceLibraryText.Get("Sources");
        CopyButton.Content = ReferenceLibraryText.Get("CopyTerm");
        OpenSourceButton.Content = ReferenceLibraryText.Get("OpenSource");
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
            _document = _pack is null ? null : ReferenceDataPackService.ParseTerminology(_pack.DataJson);
            _selected = null;
            RenderSource();
            RebuildFilters();
            RefreshResults();
        }
        catch (Exception exception)
        {
            AppLogger.Default.Error(nameof(PlanningTerminologyPage), "terminology_pack_load_failed", exception);
            _pack = null; _document = null; _selected = null;
            RenderSource(); RebuildFilters(); RefreshResults();
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
        SourceMetaText.Text = ReferenceLibraryText.Get("PackMeta", _pack.State.ArchiveFileName, _document.Terms.Count, _pack.Manifest.SchemaVersion);
    }

    private void RebuildFilters()
    {
        var jurisdiction = (JurisdictionBox.SelectedItem as Choice)?.Value;
        var category = (CategoryBox.SelectedItem as Choice)?.Value;
        var terms = _document?.Terms ?? [];
        _loading = true;
        JurisdictionBox.ItemsSource = new[] { new Choice(string.Empty, ReferenceLibraryText.Get("AllJurisdictions")) }
            .Concat(terms.Select(term => term.Jurisdiction).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value).Select(value => new Choice(value, value))).ToArray();
        CategoryBox.ItemsSource = new[] { new Choice(string.Empty, ReferenceLibraryText.Get("AllCategories")) }
            .Concat(terms.Select(term => term.Category).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value).Select(value => new Choice(value, value))).ToArray();
        SelectChoice(JurisdictionBox, jurisdiction); SelectChoice(CategoryBox, category);
        _loading = false;
    }

    private static void SelectChoice(ComboBox box, string? value)
    {
        var choices = box.ItemsSource?.Cast<Choice>().ToArray() ?? [];
        box.SelectedItem = choices.FirstOrDefault(choice => string.Equals(choice.Value, value, StringComparison.Ordinal)) ?? choices.FirstOrDefault();
    }

    private void RefreshResults()
    {
        var terms = _document?.Terms ?? [];
        var query = SearchBox.Text.Trim();
        var jurisdiction = (JurisdictionBox.SelectedItem as Choice)?.Value;
        var category = (CategoryBox.SelectedItem as Choice)?.Value;
        var aliasesByTerm = BuildAliasesByTerm();
        var results = terms.Where(term =>
                (string.IsNullOrWhiteSpace(jurisdiction) || term.Jurisdiction == jurisdiction) &&
                (string.IsNullOrWhiteSpace(category) || term.Category == category) &&
                (query.Length == 0 || BuildSearchText(term, aliasesByTerm.GetValueOrDefault(term.Id, [])).Contains(query, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(term => term.Id)
            .Select(term => new TermListItem(term, ShortJurisdiction(term.Jurisdiction) + " · " + term.Category))
            .ToArray();
        TermsList.ItemsSource = results;
        CountText.Text = ReferenceLibraryText.Get("TermCount", terms.Count, results.Length);
        ListCountBadge.Text = results.Length.ToString();
        EmptyText.Text = results.Length == 0 ? (_pack is null ? ReferenceLibraryText.Get("NoDataPackHint") : ReferenceLibraryText.Get("NoResults")) : string.Empty;
        if (results.Length > 0)
        {
            var target = _selected is null ? results[0] : results.FirstOrDefault(item => item.Term.StableId == _selected.StableId) ?? results[0];
            TermsList.SelectedItem = target;
        }
        else
        {
            _selected = null;
            DetailPanel.Visibility = Visibility.Collapsed;
        }
    }

    private Dictionary<int, List<string>> BuildAliasesByTerm()
    {
        var result = new Dictionary<int, List<string>>();
        if (_document is null) return result;
        foreach (var alias in _document.Aliases)
        {
            var termId = ReadInt(alias, "termId", "termID", "id");
            var text = ReadString(alias, "alias", "value", "text", "name");
            if (termId is null || string.IsNullOrWhiteSpace(text)) continue;
            if (!result.TryGetValue(termId.Value, out var values)) result[termId.Value] = values = [];
            values.Add(text);
        }
        return result;
    }

    private void OnFilterChanged(object sender, TextChangedEventArgs e) { if (!_loading) RefreshResults(); }
    private void OnSelectionFilterChanged(object sender, SelectionChangedEventArgs e) { if (!_loading) RefreshResults(); }

    private void OnResetFiltersClick(object sender, RoutedEventArgs e)
    {
        _loading = true; SearchBox.Text = string.Empty; JurisdictionBox.SelectedIndex = 0; CategoryBox.SelectedIndex = 0; _loading = false; RefreshResults();
    }

    private void OnTermSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TermsList.SelectedItem is not TermListItem item) return;
        _selected = item.Term;
        RenderDetail(item.Term);
    }

    private void RenderDetail(PlanningTerminologyRecord term)
    {
        DetailZh.Text = term.ZhCN;
        DetailJa.Text = string.IsNullOrWhiteSpace(term.JaReading) ? term.JaJP : $"{term.JaJP}（{term.JaReading}）";
        DetailEn.Text = term.EnUS;
        JurisdictionBadge.Text = ShortJurisdiction(term.Jurisdiction);
        EquivalenceBadge.Text = EquivalenceLabel(term.Equivalence);
        DefinitionText.Text = CurrentDefinition(term);

        var comparison = FindComparison(term);
        ComparisonContext.Text = comparison.Context;
        ComparisonText.Text = comparison.Text;
        ComparisonCard.Visibility = string.IsNullOrWhiteSpace(comparison.Text) ? Visibility.Collapsed : Visibility.Visible;
        ComparisonHeader.Visibility = ComparisonCard.Visibility;

        var related = FindRelatedTerms(term).Select(relatedTerm => new RelatedItem(relatedTerm, DisplayPrimary(relatedTerm))).ToArray();
        RelatedList.ItemsSource = related;
        RelatedHeader.Visibility = related.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        RelatedList.Visibility = related.Length == 0 ? Visibility.Collapsed : Visibility.Visible;

        var source = FindFirstSource(term);
        _selectedSourceUrl = source.Url;
        SourceTitle.Text = source.Title;
        SourceStatus.Text = source.Status;
        var hasSource = !string.IsNullOrWhiteSpace(source.Title) || !string.IsNullOrWhiteSpace(source.Url);
        SourcesHeader.Visibility = hasSource ? Visibility.Visible : Visibility.Collapsed;
        SourceCard.Visibility = hasSource ? Visibility.Visible : Visibility.Collapsed;
        OpenSourceButton.Visibility = ExternalLinkService.IsSafeHttpUri(source.Url, out _) ? Visibility.Visible : Visibility.Collapsed;
        DetailPanel.Visibility = Visibility.Visible;
    }

    private string CurrentDefinition(PlanningTerminologyRecord term) => _localization.CurrentLanguage.StartsWith("ja", StringComparison.OrdinalIgnoreCase) ? term.DefinitionJa : _localization.CurrentLanguage.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? term.DefinitionEn : term.DefinitionZh;

    private (string Context, string Text) FindComparison(PlanningTerminologyRecord term)
    {
        if (_document is not null)
        {
            foreach (var item in _document.HighRisk)
            {
                var raw = item.GetRawText();
                if (!raw.Contains(term.StableId, StringComparison.OrdinalIgnoreCase) && !raw.Contains(term.ZhCN, StringComparison.OrdinalIgnoreCase) && !raw.Contains(term.JaJP, StringComparison.OrdinalIgnoreCase)) continue;
                var note = _localization.CurrentLanguage.StartsWith("ja", StringComparison.OrdinalIgnoreCase)
                    ? ReadString(item, "noteJa", "differenceJa", "note", "difference")
                    : _localization.CurrentLanguage.StartsWith("en", StringComparison.OrdinalIgnoreCase)
                        ? ReadString(item, "noteEn", "differenceEn", "note", "difference")
                        : ReadString(item, "noteZh", "differenceZh", "note", "difference");
                if (!string.IsNullOrWhiteSpace(note)) return (ReadString(item, "context", "jurisdiction", "label") ?? EquivalenceLabel(term.Equivalence), note);
            }
        }
        return (EquivalenceLabel(term.Equivalence), EquivalenceExplanation(term.Equivalence));
    }

    private IReadOnlyList<PlanningTerminologyRecord> FindRelatedTerms(PlanningTerminologyRecord term)
    {
        if (_document is null) return [];
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var edge in _document.Edges)
        {
            var source = ReadEndpoint(edge, "sourceId", "source", "from");
            var target = ReadEndpoint(edge, "targetId", "target", "to");
            if (EndpointMatches(source, term)) { if (!string.IsNullOrWhiteSpace(target)) ids.Add(target); }
            else if (EndpointMatches(target, term)) { if (!string.IsNullOrWhiteSpace(source)) ids.Add(source); }
        }
        return _document.Terms.Where(candidate => candidate.Id != term.Id && ids.Any(id => EndpointMatches(id, candidate))).Take(8).ToArray();
    }

    private SourceInfo FindFirstSource(PlanningTerminologyRecord term)
    {
        if (_document is null) return new(string.Empty, string.Empty, string.Empty);
        var sourceIds = new List<string>();
        if (term.Extra.TryGetValue("sourceIds", out var ids) && ids.ValueKind == JsonValueKind.Array)
            sourceIds.AddRange(ids.EnumerateArray().Select(ValueAsString).Where(value => !string.IsNullOrWhiteSpace(value))!);
        foreach (var source in _document.Sources)
        {
            var id = ReadString(source, "id", "sourceId", "stableId");
            if (sourceIds.Count > 0 && !sourceIds.Contains(id ?? string.Empty, StringComparer.OrdinalIgnoreCase)) continue;
            var title = ReadString(source, "title", "name", "authority", "publisher") ?? "UrbanPlanToolbox_Data";
            var status = string.Join(" · ", new[] { ReadString(source, "status", "reviewStatus"), ReadString(source, "verifiedDate", "lastReviewed", "date") }.Where(value => !string.IsNullOrWhiteSpace(value)));
            var url = ReadString(source, "url", "officialUrl", "sourceUrl") ?? string.Empty;
            return new(title, status, url);
        }
        return new(string.Empty, string.Empty, string.Empty);
    }

    private void OnRelatedClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: RelatedItem item }) return;
        var match = TermsList.Items.OfType<TermListItem>().FirstOrDefault(card => card.Term.StableId == item.Term.StableId);
        if (match is not null) TermsList.SelectedItem = match;
        else { _selected = item.Term; RenderDetail(item.Term); }
    }

    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        if (_selected is null) return;
        var package = new DataPackage(); package.SetText($"{_selected.ZhCN}\n{_selected.JaJP}\n{_selected.EnUS}"); Clipboard.SetContent(package); ShowStatus(ReferenceLibraryText.Get("Copied"), InfoBarSeverity.Success);
    }

    private async void OnOpenSourceClick(object sender, RoutedEventArgs e)
    {
        if (!ExternalLinkService.IsSafeHttpUri(_selectedSourceUrl, out _) || !await ExternalLinkService.OpenAsync(_selectedSourceUrl)) ShowStatus(ReferenceLibraryText.Get("PackFailed", "URL"), InfoBarSeverity.Error);
    }

    private async void OnCheckUpdateClick(object sender, RoutedEventArgs e)
    {
        if (_busy) return; SetBusy(true);
        try { if (await ReferenceDataPackPageCoordinator.CheckAndInstallUpdateAsync(Root, PackId, StatusBar)) await ReloadAsync(); CloudVersionText.Text = await ReferenceDataPackPageCoordinator.GetCloudVersionTextAsync(PackId); }
        finally { SetBusy(false); }
    }

    private async void OnManageSourceClick(object sender, RoutedEventArgs e)
    {
        if (_busy) return; SetBusy(true);
        try { if (await ReferenceDataPackPageCoordinator.ManageAsync(Root, PackId, StatusBar)) await ReloadAsync(); }
        finally { SetBusy(false); }
    }

    private void OnBackClick(object sender, RoutedEventArgs e) { if (Frame.CanGoBack) Frame.GoBack(); }

    private void OnRootSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var narrow = e.NewSize.Width > 0 && e.NewSize.Width < 900;
        if (narrow)
        {
            ContentGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star); ContentGrid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
            ContentGrid.RowDefinitions[0].Height = new GridLength(Math.Clamp(Root.ActualHeight * 0.38, 260, 360)); ContentGrid.RowDefinitions[1].Height = new GridLength(1, GridUnitType.Star);
            Grid.SetRow(ListPanel, 0); Grid.SetColumn(ListPanel, 0); Grid.SetColumnSpan(ListPanel, 2); Grid.SetRow(DetailPanel, 1); Grid.SetColumn(DetailPanel, 0); Grid.SetColumnSpan(DetailPanel, 2);
        }
        else
        {
            ContentGrid.RowDefinitions[0].Height = new GridLength(1, GridUnitType.Star); ContentGrid.RowDefinitions[1].Height = new GridLength(0);
            ContentGrid.ColumnDefinitions[0].Width = new GridLength(0.75, GridUnitType.Star); ContentGrid.ColumnDefinitions[1].Width = new GridLength(1.25, GridUnitType.Star);
            Grid.SetRow(ListPanel, 0); Grid.SetColumn(ListPanel, 0); Grid.SetColumnSpan(ListPanel, 1); Grid.SetRow(DetailPanel, 0); Grid.SetColumn(DetailPanel, 1); Grid.SetColumnSpan(DetailPanel, 1);
        }
    }

    private void SetBusy(bool value) { _busy = value; HeaderCheckButton.IsEnabled = CheckButton.IsEnabled = ManageButton.IsEnabled = !value; }
    private void ShowStatus(string message, InfoBarSeverity severity) { StatusBar.Message = message; StatusBar.Severity = severity; StatusBar.IsOpen = true; }

    private string DisplayPrimary(PlanningTerminologyRecord term) => _localization.CurrentLanguage.StartsWith("ja", StringComparison.OrdinalIgnoreCase) ? term.JaJP : _localization.CurrentLanguage.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? term.EnUS : term.ZhCN;
    private static string BuildSearchText(PlanningTerminologyRecord term, IReadOnlyList<string> aliases) => string.Join('\n', new[] { term.ZhCN, term.JaJP, term.JaReading, term.EnUS, term.Category, term.Jurisdiction, term.DefinitionZh, term.DefinitionJa, term.DefinitionEn }.Concat(aliases));
    private static string ShortJurisdiction(string value) => value.StartsWith("中国", StringComparison.OrdinalIgnoreCase) ? "CN" : value.StartsWith("日本", StringComparison.OrdinalIgnoreCase) ? "JP" : value.StartsWith("通用", StringComparison.OrdinalIgnoreCase) ? "General" : value;

    private string EquivalenceLabel(string value)
    {
        var key = value switch { "exact" => "Terminology_Exact", "approximate" => "Terminology_Approximate", "translation-only" => "Terminology_TranslationOnly", _ => "Terminology_None" };
        var label = _localization.GetString(key);
        return label.StartsWith("!", StringComparison.Ordinal) ? value : label;
    }

    private string EquivalenceExplanation(string value)
    {
        var ja = _localization.CurrentLanguage.StartsWith("ja", StringComparison.OrdinalIgnoreCase);
        var en = _localization.CurrentLanguage.StartsWith("en", StringComparison.OrdinalIgnoreCase);
        return value switch
        {
            "exact" => en ? "The mapped terms are treated as a high-confidence conceptual match in this data pack." : ja ? "このデータパックでは高い確度で対応する概念として扱います。" : "该数据包将这些术语视为高置信度的概念对应。",
            "approximate" => en ? "The terms are comparable but differ by institution, scope or planning context; avoid direct substitution." : ja ? "制度・範囲・計画文脈に差があるため、単純な置換は避けてください。" : "术语具有可比性，但制度、范围或规划语境存在差异，不宜直接互换。",
            "translation-only" => en ? "This mapping is provided for translation and does not assert institutional equivalence." : ja ? "翻訳上の対応であり、制度的な同等性を示すものではありません。" : "该对应仅用于翻译，不表示制度层面的等同。",
            _ => en ? "No direct cross-context equivalence is asserted." : ja ? "異なる文脈間での直接的な同等性は設定されていません。" : "该术语未声明跨语境的一一对应关系。"
        };
    }

    private static string? ReadEndpoint(JsonElement element, params string[] names)
    {
        foreach (var name in names) if (element.TryGetProperty(name, out var property)) return ValueAsString(property);
        return null;
    }
    private static bool EndpointMatches(string? endpoint, PlanningTerminologyRecord term) => !string.IsNullOrWhiteSpace(endpoint) && (string.Equals(endpoint, term.Id.ToString(), StringComparison.OrdinalIgnoreCase) || string.Equals(endpoint, term.StableId, StringComparison.OrdinalIgnoreCase) || string.Equals(endpoint, term.ZhCN, StringComparison.OrdinalIgnoreCase) || string.Equals(endpoint, term.JaJP, StringComparison.OrdinalIgnoreCase));
    private static int? ReadInt(JsonElement element, params string[] names) { foreach (var name in names) if (element.TryGetProperty(name, out var property) && property.TryGetInt32(out var value)) return value; return null; }
    private static string? ReadString(JsonElement element, params string[] names) { foreach (var name in names) if (element.TryGetProperty(name, out var property)) { var value = ValueAsString(property); if (!string.IsNullOrWhiteSpace(value)) return value; } return null; }
    private static string? ValueAsString(JsonElement element) => element.ValueKind switch { JsonValueKind.String => element.GetString(), JsonValueKind.Number => element.GetRawText(), _ => null };

    private sealed record Choice(string Value, string Display);
    private sealed record TermListItem(PlanningTerminologyRecord Term, string Metadata) { public string ZhCN => Term.ZhCN; public string JaJP => Term.JaJP; public string EnUS => Term.EnUS; }
    private sealed record RelatedItem(PlanningTerminologyRecord Term, string Display);
    private sealed record SourceInfo(string Title, string Status, string Url);
}
