using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Models.Tools;
using UrbanPlanToolbox.Services;

namespace UrbanPlanToolbox.Views;

public sealed partial class RegulationsIndexPage : Page
{
    private RegulationsIndexService? _service;
    private readonly ILocalizationService _localization = LocalizationService.Default;
    private RegulationEntry? _selectedEntry;
    private bool _loaded;
    private bool _openingLink;

    public RegulationsIndexPage()
    {
        InitializeComponent();
        TitleText.Text = T("Tool_RegulationsIndex_Name");
        DescriptionText.Text = T("Tool_RegulationsIndex_Description");
        SearchBox.PlaceholderText = T("Regulations_SearchPlaceholder");
        Loaded += OnLoaded;
    }

    private string T(string key) => _localization.GetString(key);
    private string T(string key, params object[] args) => string.Format(_localization.GetString(key), args);
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_loaded) return;
        _loaded = true;
        try
        {
            _service = RegulationsIndexService.LoadPackaged();
            var data = _service.Data;
            StatsText.Text = T("Regulations_Stats", data.Entries.Count, data.Entries.Count(entry => entry.Region == "中国"), data.Entries.Count(entry => entry.Region == "日本"), data.Entries.Count(entry => entry.Region == "美国"), data.Entries.Count(entry => entry.Region == "欧盟/欧洲"));
            NoticeText.Text = T("Regulations_Notice", data.SourceVerifiedDate);
            RegulationsHeader.Text = T("Regulations_RegulationsHeader");
            PortalsHeader.Text = T("Regulations_OfficialPortals");
            NotesHeader.Text = T("Regulations_FieldNotes");
            RegionBox.ItemsSource = new[] { T("Regulations_AllRegions") }.Concat(data.Entries.Select(e => e.Region).Distinct()).ToArray();
            TopicBox.ItemsSource = new[] { T("Regulations_AllTopics") }.Concat(data.Entries.Select(e => e.Topic).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct()!).ToArray();
            PortalsList.ItemsSource = data.OfficialPortals.Select(CreatePortalItem).ToArray();
            NotesList.ItemsSource = data.FieldNotes.Select(note => $"{note.Topic}: {note.Note}").ToArray();
            RegionBox.SelectedIndex = 0;
            TopicBox.SelectedIndex = 0;
            Refresh();
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Regulations index load failed: {exception}");
            NoticeText.Text = T("Regulations_LoadFailed");
            EntriesList.ItemsSource = Array.Empty<RegulationListItem>();
        }
    }

    private OfficialPortalListItem CreatePortalItem(OfficialPortal portal)
    {
        var valid = ExternalLinkService.IsSafeHttpUri(portal.Url, out _);
        return new OfficialPortalListItem
        {
            Portal = portal,
            IsValidUrl = valid,
            UrlDisplay = portal.Url?.Trim() ?? string.Empty,
            AutomationName = $"{portal.PlatformName} {T("Regulations_OpenOfficial")}",
            OpenButtonText = valid ? T("Regulations_OpenOfficial") : T("Regulations_LinkUnavailable"),
            OpenButtonToolTip = valid ? T("Regulations_OpenOfficial") : T("Regulations_LinkUnavailable"),
            StatusText = valid ? string.Empty : T("Regulations_LinkUnavailable")
        };
    }

    private void OnTextChanged(object sender, TextChangedEventArgs args) => Refresh();
    private void OnSelectionChanged(object sender, SelectionChangedEventArgs args) => Refresh();

    private void Refresh()
    {
        if (_service is null) return;
        var region = RegionBox.SelectedIndex <= 0 ? null : RegionBox.SelectedItem?.ToString();
        var topic = TopicBox.SelectedIndex <= 0 ? null : TopicBox.SelectedItem?.ToString();
        var entries = _service.Search(SearchBox.Text, region, topic: topic);
        EntriesList.ItemsSource = entries.Select(entry => new RegulationListItem(
            entry,
            $"{entry.Id}. {entry.ChineseTitle ?? entry.OriginalTitle}",
            $"{entry.Region} · {entry.DocumentLevel} · {entry.Topic}",
            entry.ScopeAndPurpose,
            $"{entry.Id}. {entry.ChineseTitle ?? entry.OriginalTitle}"))
            .ToArray();
    }

    private void OnRegulationButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: RegulationListItem item }) ShowDetail(item.Entry);
    }

    private void OnEntryClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is RegulationListItem item) ShowDetail(item.Entry);
    }

    private void ShowDetail(RegulationEntry entry)
    {
        _selectedEntry = entry;
        DetailHeader.Text = entry.ChineseTitle ?? entry.OriginalTitle;
        DetailText.Text = string.Join(Environment.NewLine, new[]
        {
            $"{T("Regulations_OriginalTitle")}: {entry.OriginalTitle}",
            $"{T("Regulations_Identifier")}: {entry.IdentifierOrYear ?? "—"}",
            $"{T("Regulations_Scope")}: {entry.ScopeAndPurpose}",
            $"{T("Regulations_Effect")}: {entry.EffectOrAdoption ?? "—"}",
            $"{T("Regulations_Verified")}: {entry.VerifiedDate}"
        });
        DetailOpenButton.Content = T("Regulations_OpenOfficial");
        DetailOpenButton.Visibility = ExternalLinkService.IsSafeHttpUri(entry.OfficialUrl, out _) ? Visibility.Visible : Visibility.Collapsed;
        DetailDownloadButton.Content = T("Regulations_OpenPdf");
        DetailDownloadButton.Visibility = ExternalLinkService.IsSafeHttpUri(entry.DownloadUrl, out _) ? Visibility.Visible : Visibility.Collapsed;
        DetailCloseButton.Content = T("Regulations_CloseDetail");
        DetailPanel.Visibility = Visibility.Visible;
    }

    private async void OnOpenPortalClick(object sender, RoutedEventArgs e)
    {
        if (_openingLink || sender is not Button { Tag: OfficialPortalListItem { IsValidUrl: true } item }) return;
        await OpenLinkAsync(item.Portal.Url);
    }

    private async void OnDetailOpenClick(object sender, RoutedEventArgs e) => await OpenLinkAsync(_selectedEntry?.OfficialUrl);
    private async void OnDetailDownloadClick(object sender, RoutedEventArgs e) => await OpenLinkAsync(_selectedEntry?.DownloadUrl);
    private void OnDetailCloseClick(object sender, RoutedEventArgs e) => DetailPanel.Visibility = Visibility.Collapsed;

    private async Task OpenLinkAsync(string? url)
    {
        if (_openingLink || !ExternalLinkService.IsSafeHttpUri(url, out _))
        {
            ShowLinkError();
            return;
        }

        _openingLink = true;
        LinkErrorBar.IsOpen = false;
        try
        {
            if (!await ExternalLinkService.OpenAsync(url)) ShowLinkError();
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Opening external regulations link failed: {exception}");
            ShowLinkError();
        }
        finally { _openingLink = false; }
    }

    private void ShowLinkError()
    {
        LinkErrorBar.Message = T("Regulations_OpenFailed");
        LinkErrorBar.IsOpen = true;
    }
}
