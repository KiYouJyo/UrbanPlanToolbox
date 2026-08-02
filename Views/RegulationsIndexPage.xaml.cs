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
    private readonly RegulationsIndexService _service = RegulationsIndexService.LoadPackaged();
    private readonly ILocalizationService _localization = LocalizationService.Default;

    public RegulationsIndexPage()
    {
        InitializeComponent();
        TitleText.Text = T("Tool_RegulationsIndex_Name");
        DescriptionText.Text = T("Tool_RegulationsIndex_Description");
        FavoriteButton.Content = T("Regulations_Favorite");
        FavoriteButton.Click += (_, _) => FavoriteToolsService.Default.Toggle(ToolIds.RegulationsIndex);
        StatsText.Text = T("Regulations_Stats", _service.Data.Entries.Count, _service.Data.Entries.Count(entry => entry.Region == "中国"), _service.Data.Entries.Count(entry => entry.Region == "日本"), _service.Data.Entries.Count(entry => entry.Region == "美国"), _service.Data.Entries.Count(entry => entry.Region == "欧盟/欧洲"));
        NoticeText.Text = T("Regulations_Notice", _service.Data.SourceVerifiedDate);
        RegionBox.ItemsSource = new[] { T("Regulations_AllRegions") }.Concat(_service.Data.Entries.Select(e => e.Region).Distinct()).ToArray();
        TopicBox.ItemsSource = new[] { T("Regulations_AllTopics") }.Concat(_service.Data.Entries.Select(e => e.Topic).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct()!).ToArray();
        RegionBox.SelectedIndex = TopicBox.SelectedIndex = 0;
        PortalsHeader.Text = T("Regulations_OfficialPortals");
        NotesHeader.Text = T("Regulations_FieldNotes");
        PortalsList.ItemsSource = _service.Data.OfficialPortals.Select(portal => new TextBlock { Text = $"{portal.PlatformName} ({portal.Region})  {portal.Url}", TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true, Margin = new Thickness(0, 2, 0, 2) }).ToArray();
        NotesList.ItemsSource = _service.Data.FieldNotes.Select(note => new TextBlock { Text = $"{note.Topic}: {note.Note}", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 2) }).ToArray();
        Refresh();
    }

    private string T(string key) => _localization.GetString(key);
    private string T(string key, params object[] args) => string.Format(_localization.GetString(key), args);
    private void OnTextChanged(object sender, TextChangedEventArgs args) => Refresh();
    private void OnSelectionChanged(object sender, SelectionChangedEventArgs args) => Refresh();
    private void Refresh()
    {
        var region = RegionBox.SelectedIndex <= 0 ? null : RegionBox.SelectedItem?.ToString();
        var topic = TopicBox.SelectedIndex <= 0 ? null : TopicBox.SelectedItem?.ToString();
        var entries = _service.Search(SearchBox.Text, region, topic: topic);
        EntriesList.ItemsSource = entries.Select(entry => new TextBlock { Text = $"{entry.Id}. {entry.ChineseTitle ?? entry.OriginalTitle}\n{entry.Region} · {entry.DocumentLevel} · {entry.Topic}\n{entry.ScopeAndPurpose}", TextWrapping = TextWrapping.Wrap, Padding = new Thickness(8) }).ToArray();
        EntriesList.Tag = entries;
    }
    private async void OnEntryClick(object sender, ItemClickEventArgs e)
    {
        if (EntriesList.Tag is not IReadOnlyList<RegulationEntry> entries || e.ClickedItem is not TextBlock card) return;
        var index = EntriesList.Items.IndexOf(card); if (index < 0) return;
        var entry = entries[index]; var url = entry.OfficialUrl ?? entry.DownloadUrl;
        if (url is not null) await Launcher.LaunchUriAsync(new Uri(url));
        var data = new DataPackage(); data.SetText($"{entry.OriginalTitle}\n{entry.IdentifierOrYear}\n{url}"); Clipboard.SetContent(data);
    }
}
