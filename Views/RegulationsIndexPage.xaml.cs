using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Models.Tools;
using UrbanPlanToolbox.Services;

namespace UrbanPlanToolbox.Views;

public sealed partial class RegulationsIndexPage : Page
{
    private RegulationsIndexService? _service;
    private readonly ILocalizationService _localization = LocalizationService.Default;
    private bool _loaded;

    public RegulationsIndexPage()
    {
        InitializeComponent();
        TitleText.Text = T("Tool_RegulationsIndex_Name");
        DescriptionText.Text = T("Tool_RegulationsIndex_Description");
        FavoriteButton.Content = T("Regulations_Favorite");
        FavoriteButton.Click += OnFavoriteClick;
        SearchBox.PlaceholderText = T("Regulations_SearchPlaceholder");
        Loaded += OnLoaded;
    }

    private string T(string key) => _localization.GetString(key);
    private string T(string key, params object[] args) => string.Format(_localization.GetString(key), args);
    private void OnFavoriteClick(object sender, RoutedEventArgs e) => FavoriteToolsService.Default.Toggle(ToolIds.RegulationsIndex);
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
            RegionBox.ItemsSource = new[] { T("Regulations_AllRegions") }.Concat(data.Entries.Select(e => e.Region).Distinct()).ToArray();
            TopicBox.ItemsSource = new[] { T("Regulations_AllTopics") }.Concat(data.Entries.Select(e => e.Topic).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct()!).ToArray();
            PortalsHeader.Text = T("Regulations_OfficialPortals");
            NotesHeader.Text = T("Regulations_FieldNotes");
            PortalsList.ItemsSource = data.OfficialPortals.Select(portal => $"{portal.PlatformName} ({portal.Region})  {portal.Url}").ToArray();
            NotesList.ItemsSource = data.FieldNotes.Select(note => $"{note.Topic}: {note.Note}").ToArray();
            RegionBox.SelectedIndex = 0;
            TopicBox.SelectedIndex = 0;
            Refresh();
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Regulations index load failed: {exception}");
            NoticeText.Text = T("Regulations_LoadFailed");
            EntriesList.ItemsSource = Array.Empty<string>();
        }
    }
    private void OnTextChanged(object sender, TextChangedEventArgs args) => Refresh();
    private void OnSelectionChanged(object sender, SelectionChangedEventArgs args) => Refresh();
    private void Refresh()
    {
        var region = RegionBox.SelectedIndex <= 0 ? null : RegionBox.SelectedItem?.ToString();
        var topic = TopicBox.SelectedIndex <= 0 ? null : TopicBox.SelectedItem?.ToString();
        if (_service is null) return;
        var entries = _service.Search(SearchBox.Text, region, topic: topic);
        EntriesList.ItemsSource = entries.Select(entry => $"{entry.Id}. {entry.ChineseTitle ?? entry.OriginalTitle}\n{entry.Region} · {entry.DocumentLevel} · {entry.Topic}\n{entry.ScopeAndPurpose}").ToArray();
        EntriesList.Tag = entries;
    }
    private async void OnEntryClick(object sender, ItemClickEventArgs e)
    {
        if (EntriesList.Tag is not IReadOnlyList<RegulationEntry> entries) return;
        var index = e.ClickedItem is string card ? EntriesList.Items.IndexOf(card) : -1; if (index < 0) return;
        var entry = entries[index]; var url = entry.OfficialUrl ?? entry.DownloadUrl;
        if (url is not null) await Launcher.LaunchUriAsync(new Uri(url));
        var data = new DataPackage(); data.SetText($"{entry.OriginalTitle}\n{entry.IdentifierOrYear}\n{url}"); Clipboard.SetContent(data);
    }
}
