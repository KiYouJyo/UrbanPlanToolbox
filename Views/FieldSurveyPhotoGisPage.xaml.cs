using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;
using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Services;

namespace UrbanPlanToolbox.Views;

public sealed partial class FieldSurveyPhotoGisPage : Page
{
    public ObservableCollection<FieldSurveyPhoto> Photos { get; } = [];
    private readonly FieldSurveyPhotoImportService _import = new();
    private readonly FieldSurveyNamingService _naming = new();
    private readonly FieldSurveyExportService _export = new();
    private FieldSurveyPhoto? _selected;
    private string? _output;
    private bool _updatingEditor;

    public FieldSurveyPhotoGisPage()
    {
        InitializeComponent(); DataContext = this;
        var l = LocalizationService.Default;
        TitleText.Text = l.GetString("Tool_FieldSurveyPhotoGis_Name"); DescriptionText.Text = l.GetString("Tool_FieldSurveyPhotoGis_Description");
        AddButton.Content = l.GetString("FieldSurvey_AddPhotos"); RemoveButton.Content = l.GetString("FieldSurvey_Remove"); ClearButton.Content = l.GetString("FieldSurvey_Clear"); ExportButton.Content = l.GetString("FieldSurvey_Export"); SelectOutputButton.Content = l.GetString("FieldSurvey_Output");
        TagsBox.Header = l.GetString("FieldSurvey_Tags"); TagsBox.PlaceholderText = l.GetString("FieldSurvey_TagsPlaceholder"); NoteBox.Header = l.GetString("FieldSurvey_Note"); NoteBox.PlaceholderText = l.GetString("FieldSurvey_NotePlaceholder"); EmptyState.Text = l.GetString("FieldSurvey_EmptyState"); NoSelectionText.Text = l.GetString("FieldSurvey_NoSelection");
        UpdateState();
    }

    private async void OnAddPhotos(object sender, RoutedEventArgs e)
    {
        AddButton.IsEnabled = false; StatusBar.IsOpen = false;
        try { var picker = new FileOpenPicker(); foreach (var ext in new[] { ".jpg", ".jpeg", ".heic", ".heif", ".png" }) picker.FileTypeFilter.Add(ext); WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow)); var files = await picker.PickMultipleFilesAsync(); AppLogger.Default.Info("FieldSurveyPhoto", "PhotoPickerReturned", $"count={files?.Count ?? 0}"); if (files is not null) await ImportAsync(files); }
        catch (Exception ex) { AppLogger.Default.Error("FieldSurveyPhoto", "PhotoImportFailed", ex, ex.GetType().Name); Show(LocalizationService.Default.GetString("FieldSurvey_ImportFailed"), InfoBarSeverity.Error); }
        finally { AddButton.IsEnabled = true; }
    }

    private async Task ImportAsync(IEnumerable<Windows.Storage.StorageFile> files)
    {
        var result = await _import.ImportAsync(files, new Progress<int>(count => SummaryText.Text = $"{LocalizationService.Default.GetString("FieldSurvey_Reading")} {count}"));
        foreach (var photo in result.Photos) Photos.Add(photo);
        _naming.AssignIds(Photos.ToList());
        if (_selected is null && Photos.Count > 0) PhotoList.SelectedItem = Photos[0];
        UpdateState();
        if (result.Photos.Count == 0 && result.UnsupportedFiles.Count + result.FailedFiles.Count > 0) Show(LocalizationService.Default.GetString("FieldSurvey_ImportFailed"), InfoBarSeverity.Error);
        else if (result.FailedFiles.Count > 0) Show($"{result.FailedFiles.Count} photo(s) could not be read.", InfoBarSeverity.Warning);
        else if (result.DuplicateCount > 0) Show($"{result.DuplicateCount} duplicate photo(s) ignored.", InfoBarSeverity.Informational);
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PhotoList.SelectedItem is not FieldSurveyPhoto photo) { _selected = null; UpdateState(); return; }
        _selected = photo; _updatingEditor = true;
        DetailsTitle.Text = photo.Id + "  " + photo.OriginalName; MetadataText.Text = $"{photo.CapturedAt}\n{photo.Longitude?.ToString() ?? "—"}, {photo.Latitude?.ToString() ?? "—"}\n{photo.Altitude?.ToString() ?? "—"}\n{photo.Heading?.ToString() ?? "—"}\n{photo.Make} {photo.Model}\nGPS: {photo.GpsStatus}"; TagsBox.Text = string.Join(';', photo.Tags); NoteBox.Text = photo.Note; TemplateText.Text = _naming.BuildFileName(photo, "{ID}_{Date}_{Time}"); _updatingEditor = false; UpdateState();
    }

    private void OnTagsChanged(object sender, TextChangedEventArgs e) { if (_updatingEditor || _selected is null) return; _selected.Tags.Clear(); foreach (var tag in TagsBox.Text.Split([',', '，', ';', '；'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase)) _selected.Tags.Add(tag); }
    private void OnNoteChanged(object sender, TextChangedEventArgs e) { if (!_updatingEditor && _selected is not null) _selected.Note = NoteBox.Text; }
    private void OnRemove(object sender, RoutedEventArgs e) { foreach (var item in PhotoList.SelectedItems.OfType<FieldSurveyPhoto>().ToArray()) Photos.Remove(item); _selected = PhotoList.SelectedItem as FieldSurveyPhoto; UpdateState(); }
    private void OnClear(object sender, RoutedEventArgs e) { Photos.Clear(); _selected = null; UpdateState(); }
    private async void OnSelectOutput(object sender, RoutedEventArgs e) { var picker = new FolderPicker(); picker.FileTypeFilter.Add("*"); WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow)); var folder = await picker.PickSingleFolderAsync(); if (folder is not null) SetOutputFolder(folder.Path); }
    private async void OnExport(object sender, RoutedEventArgs e) { if (_output is null) { await SelectOutputAsync(); if (_output is null) return; } var result = await _export.ExportAsync(Photos, new(_output)); Show(result.IsSuccess ? $"Exported {result.PhotoCount} photos and {result.GpsCount} GIS points." : result.Error ?? "Export failed.", result.IsSuccess ? InfoBarSeverity.Success : InfoBarSeverity.Error); }
    private async Task SelectOutputAsync() { var picker = new FolderPicker(); picker.FileTypeFilter.Add("*"); WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow)); var folder = await picker.PickSingleFolderAsync(); if (folder is not null) SetOutputFolder(folder.Path); }
    private void SetOutputFolder(string path) { _output = path; OutputPathText.Text = path; OutputPathText.Visibility = Visibility.Visible; }
    private void OnDragOver(object sender, DragEventArgs e) => e.AcceptedOperation = DataPackageOperation.Copy;
    private async void OnDrop(object sender, DragEventArgs e) { if (e.DataView.Contains(StandardDataFormats.StorageItems)) { var items = await e.DataView.GetStorageItemsAsync(); await ImportAsync(items.OfType<Windows.Storage.StorageFile>()); } }
    private void UpdateState() { EmptyState.Visibility = Photos.Count == 0 ? Visibility.Visible : Visibility.Collapsed; PhotoList.Visibility = Photos.Count == 0 ? Visibility.Collapsed : Visibility.Visible; DetailsPanel.Visibility = _selected is null ? Visibility.Visible : Visibility.Visible; NoSelectionText.Visibility = _selected is null ? Visibility.Visible : Visibility.Collapsed; DetailsTitle.Visibility = _selected is null ? Visibility.Collapsed : Visibility.Visible; MetadataText.Visibility = _selected is null ? Visibility.Collapsed : Visibility.Visible; TagsBox.Visibility = _selected is null ? Visibility.Collapsed : Visibility.Visible; NoteBox.Visibility = _selected is null ? Visibility.Collapsed : Visibility.Visible; TemplateText.Visibility = _selected is null ? Visibility.Collapsed : Visibility.Visible; RemoveButton.IsEnabled = PhotoList.SelectedItems.Count > 0; ExportButton.IsEnabled = Photos.Count > 0; SummaryText.Text = $"{Photos.Count} / GPS {Photos.Count(photo => photo.GpsStatus == PhotoGpsStatus.Valid)}"; }
    private void Show(string message, InfoBarSeverity severity) { StatusBar.Message = message; StatusBar.Severity = severity; StatusBar.IsOpen = true; }
}
