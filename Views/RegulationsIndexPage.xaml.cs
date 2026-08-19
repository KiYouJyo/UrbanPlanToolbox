using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Text.Json;
using Windows.ApplicationModel.DataTransfer;
using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Services;

namespace UrbanPlanToolbox.Views;

public sealed partial class RegulationsIndexPage : Page
{
    private const string PackId = ReferenceDataPackIds.PlanningRegulations;
    private readonly ILocalizationService _localization = LocalizationService.Default;
    private ReferenceDataPackContent? _pack;
    private PlanningRegulationsPackDocument? _document;
    private PlanningRegulationRecord? _selected;
    private bool _loading;
    private bool _busy;

    public RegulationsIndexPage()
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
        TitleText.Text = _localization.GetString("Tool_RegulationsIndex_Name");
        DescriptionText.Text = _localization.GetString("Tool_RegulationsIndex_Description");
        HeaderCheckButton.Content = ReferenceLibraryText.Get("CheckDataUpdate");
        CurrentSourceLabel.Text = ReferenceLibraryText.Get("CurrentSource");
        CheckButton.Content = ReferenceLibraryText.Get("CheckUpdate");
        ManageButton.Content = ReferenceLibraryText.Get("ManageSource");
        SearchBox.PlaceholderText = ReferenceLibraryText.Get("RegSearch");
        ResetButton.Content = ReferenceLibraryText.Get("ResetFilters");
        ListHeader.Text = ReferenceLibraryText.Get("RegEntries");
        SummaryHeader.Text = ReferenceLibraryText.Get("Summary");
        TagsHeader.Text = ReferenceLibraryText.Get("ApplicableTags");
        SourcesHeader.Text = ReferenceLibraryText.Get("Sources");
        OpenOfficialButton.Content = ReferenceLibraryText.Get("OpenOfficial");
        CopyButton.Content = ReferenceLibraryText.Get("Copy");
        DownloadButton.Content = ReferenceLibraryText.Get("DownloadSource");
        BrowserButton.Content = ReferenceLibraryText.Get("OpenBrowser");
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
            _document = _pack is null ? null : ReferenceDataPackService.ParseRegulations(_pack.DataJson);
            _selected = null;
            RenderSource();
            RebuildFilters();
            RefreshResults();
        }
        catch (Exception exception)
        {
            AppLogger.Default.Error(nameof(RegulationsIndexPage), "regulations_pack_load_failed", exception);
            _pack = null;
            _document = null;
            _selected = null;
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
        var region = (RegionBox.SelectedItem as Choice)?.Value;
        var topic = (TopicBox.SelectedItem as Choice)?.Value;
        var status = (StatusBox.SelectedItem as Choice)?.Value;
        var entries = _document?.Entries ?? [];
        _loading = true;
        RegionBox.ItemsSource = new[] { new Choice(string.Empty, ReferenceLibraryText.Get("AllRegions")) }.Concat(entries.Select(entry => entry.Region).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value).Select(value => new Choice(value, value))).ToArray();
        TopicBox.ItemsSource = new[] { new Choice(string.Empty, ReferenceLibraryText.Get("AllTopics")) }.Concat(entries.Select(entry => entry.Topic).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value).Select(value => new Choice(value, value))).ToArray();
        StatusBox.ItemsSource = new[] { new Choice(string.Empty, ReferenceLibraryText.Get("AllStatus")), new Choice("current", ReferenceLibraryText.Get("Current")), new Choice("draft", ReferenceLibraryText.Get("Draft")), new Choice("archived", ReferenceLibraryText.Get("Archived")) };
        SelectChoice(RegionBox, region); SelectChoice(TopicBox, topic); SelectChoice(StatusBox, status);
        _loading = false;
    }

    private static void SelectChoice(ComboBox box, string? value)
    {
        var choices = box.ItemsSource?.Cast<Choice>().ToArray() ?? [];
        box.SelectedItem = choices.FirstOrDefault(choice => string.Equals(choice.Value, value, StringComparison.Ordinal)) ?? choices.FirstOrDefault();
    }

    private void RefreshResults()
    {
        var all = _document?.Entries ?? [];
        var query = SearchBox.Text.Trim();
        var region = (RegionBox.SelectedItem as Choice)?.Value;
        var topic = (TopicBox.SelectedItem as Choice)?.Value;
        var status = (StatusBox.SelectedItem as Choice)?.Value;
        var results = all.Where(entry =>
                (string.IsNullOrWhiteSpace(region) || entry.Region == region) &&
                (string.IsNullOrWhiteSpace(topic) || entry.Topic == topic) &&
                (string.IsNullOrWhiteSpace(status) || GetStatus(entry) == status) &&
                (query.Length == 0 || BuildSearchText(entry).Contains(query, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(entry => entry.Id)
            .Select(entry => new RegulationListItem(entry, DisplayTitle(entry), $"{entry.IdentifierOrYear ?? entry.DocumentLevel} · {entry.Region} · {StatusLabel(GetStatus(entry))}", entry.ScopeAndPurpose))
            .ToArray();
        EntriesList.ItemsSource = results;
        CountText.Text = ReferenceLibraryText.Get("RegCount", all.Count, results.Length);
        ListCountBadge.Text = results.Length.ToString();
        EmptyText.Text = results.Length == 0 ? (_pack is null ? ReferenceLibraryText.Get("NoDataPackHint") : ReferenceLibraryText.Get("NoResults")) : string.Empty;
        if (results.Length > 0)
        {
            var target = _selected is null ? results[0] : results.FirstOrDefault(item => item.Entry.StableId == _selected.StableId) ?? results[0];
            EntriesList.SelectedItem = target;
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
        RegionBox.SelectedIndex = 0; TopicBox.SelectedIndex = 0; StatusBox.SelectedIndex = 0;
        _loading = false;
        RefreshResults();
    }

    private void OnEntrySelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (EntriesList.SelectedItem is not RegulationListItem item) return;
        _selected = item.Entry;
        RenderDetail(item.Entry);
    }

    private void RenderDetail(PlanningRegulationRecord entry)
    {
        DetailTitle.Text = DisplayTitle(entry);
        DetailStatus.Text = StatusLabel(GetStatus(entry));
        DetailIdentifier.Text = entry.IdentifierOrYear ?? entry.DocumentLevel;
        DetailMeta.Text = string.Join(" · ", new[] { entry.Region, entry.JurisdictionLevel, entry.DocumentLevel, entry.EffectOrAdoption }.Where(value => !string.IsNullOrWhiteSpace(value)));
        DetailSummary.Text = entry.ScopeAndPurpose;
        TagsList.ItemsSource = new[] { entry.Topic, entry.JurisdictionLevel, entry.DocumentLevel }.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).ToArray();
        SourceTitle.Text = $"{GetSourceAuthority()} · {DisplayTitle(entry)}";
        SourceStatus.Text = $"{ReferenceLibraryText.Get("Verified")} · {entry.VerifiedDate}";
        OpenOfficialButton.Visibility = ExternalLinkService.IsSafeHttpUri(entry.OfficialUrl, out _) ? Visibility.Visible : Visibility.Collapsed;
        BrowserButton.Visibility = OpenOfficialButton.Visibility;
        DownloadButton.Visibility = ExternalLinkService.IsSafeHttpUri(entry.DownloadUrl, out _) ? Visibility.Visible : Visibility.Collapsed;
        DetailPanel.Visibility = Visibility.Visible;
    }

    private string GetSourceAuthority()
    {
        if (_document is not null && _document.Source.ValueKind == JsonValueKind.Object)
        {
            foreach (var key in new[] { "authority", "name", "sourceName", "publisher" })
                if (_document.Source.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())) return value.GetString()!;
        }
        return "UrbanPlanToolbox_Data";
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

    private async void OnOpenOfficialClick(object sender, RoutedEventArgs e) => await OpenAsync(_selected?.OfficialUrl);
    private async void OnDownloadClick(object sender, RoutedEventArgs e) => await OpenAsync(_selected?.DownloadUrl);

    private async Task OpenAsync(string? url)
    {
        if (!ExternalLinkService.IsSafeHttpUri(url, out _) || !await ExternalLinkService.OpenAsync(url)) ShowStatus(ReferenceLibraryText.Get("PackFailed", "URL"), InfoBarSeverity.Error);
    }

    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        if (_selected is null) return;
        var package = new DataPackage();
        package.SetText(string.Join(Environment.NewLine, new[] { DisplayTitle(_selected), _selected.IdentifierOrYear, _selected.OfficialUrl }.Where(value => !string.IsNullOrWhiteSpace(value))));
        Clipboard.SetContent(package);
        ShowStatus(ReferenceLibraryText.Get("Copied"), InfoBarSeverity.Success);
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
            ContentGrid.RowDefinitions[0].Height = new GridLength(1, GridUnitType.Star); ContentGrid.RowDefinitions[1].Height = new GridLength(0);
            ContentGrid.ColumnDefinitions[0].Width = new GridLength(0.9, GridUnitType.Star); ContentGrid.ColumnDefinitions[1].Width = new GridLength(1.1, GridUnitType.Star);
            Grid.SetRow(ListPanel, 0); Grid.SetColumn(ListPanel, 0); Grid.SetColumnSpan(ListPanel, 1);
            Grid.SetRow(DetailPanel, 0); Grid.SetColumn(DetailPanel, 1); Grid.SetColumnSpan(DetailPanel, 1);
        }
    }

    private void SetBusy(bool value)
    {
        _busy = value;
        HeaderCheckButton.IsEnabled = CheckButton.IsEnabled = ManageButton.IsEnabled = !value;
    }

    private void ShowStatus(string message, InfoBarSeverity severity) { StatusBar.Message = message; StatusBar.Severity = severity; StatusBar.IsOpen = true; }
    private static string DisplayTitle(PlanningRegulationRecord entry) => string.IsNullOrWhiteSpace(entry.ChineseTitle) ? entry.OriginalTitle : entry.ChineseTitle;
    private static string BuildSearchText(PlanningRegulationRecord entry) => string.Join('\n', entry.OriginalTitle, entry.ChineseTitle, entry.IdentifierOrYear, entry.Region, entry.JurisdictionLevel, entry.Topic, entry.DocumentLevel, entry.ScopeAndPurpose, entry.EffectOrAdoption, entry.SearchKeywords);
    private static string GetStatus(PlanningRegulationRecord entry)
    {
        var value = entry.EffectOrAdoption;
        if (value.Contains("废", StringComparison.OrdinalIgnoreCase) || value.Contains("失効", StringComparison.OrdinalIgnoreCase) || value.Contains("repeal", StringComparison.OrdinalIgnoreCase) || value.Contains("historic", StringComparison.OrdinalIgnoreCase)) return "archived";
        if (value.Contains("草案", StringComparison.OrdinalIgnoreCase) || value.Contains("案", StringComparison.OrdinalIgnoreCase) || value.Contains("draft", StringComparison.OrdinalIgnoreCase)) return "draft";
        return "current";
    }
    private static string StatusLabel(string status) => ReferenceLibraryText.Get(status switch { "draft" => "Draft", "archived" => "Archived", _ => "Current" });

    private sealed record Choice(string Value, string Display);
    private sealed record RegulationListItem(PlanningRegulationRecord Entry, string Title, string Metadata, string Summary);
}
