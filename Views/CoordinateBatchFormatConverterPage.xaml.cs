using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;

namespace UrbanPlanToolbox.Views;

public sealed partial class CoordinateBatchFormatConverterPage : Page
{
    private readonly CoordinateBatchConversionService _service = new();
    private readonly ObservableCollection<CoordinateBatchDisplayRow> _rows = [];
    private readonly ILocalizationService _localization = LocalizationService.Default;

    public CoordinateBatchFormatConverterPage()
    {
        InitializeComponent(); ResultsList.ItemsSource = _rows;
        TitleText.Text = _localization.GetString("Tool_CoordinateBatchFormatConverter_Name"); DescriptionText.Text = _localization.GetString("Tool_CoordinateBatchFormatConverter_Description");
        PasteButton.Content = _localization.GetString("CoordinateBatch_Paste"); ImportButton.Content = _localization.GetString("CoordinateBatch_Import"); ConvertButton.Content = _localization.GetString("CoordinateBatch_Convert"); CopyButton.Content = _localization.GetString("CoordinateBatch_Copy"); ExportButton.Content = _localization.GetString("CoordinateBatch_Export"); GisHintText.Text = _localization.GetString("CoordinateBatch_GisHint");
        OrderBox.Header = _localization.GetString("CoordinateBatch_Order"); FormatBox.Header = _localization.GetString("CoordinateBatch_OutputFormat"); PrecisionBox.Header = _localization.GetString("CoordinateBatch_Precision");
        OrderBox.ItemsSource = Enum.GetValues<CoordinateOrder>().Select(value => new Option(value, _localization.GetString($"CoordinateBatch_Order_{value}"))).ToArray(); OrderBox.SelectedIndex = 0;
        FormatBox.ItemsSource = Enum.GetValues<CoordinateTextFormat>().Where(value => value != CoordinateTextFormat.Unknown).Select(value => new Option(value, value switch { CoordinateTextFormat.DecimalDegrees => "DD", CoordinateTextFormat.DegreesDecimalMinutes => "DDM", _ => "DMS" })).ToArray(); FormatBox.SelectedIndex = 0;
        PrecisionBox.ItemsSource = new[] { 4, 5, 6, 7, 8 }.Select(value => new Option(value, value.ToString(CultureInfo.InvariantCulture))).ToArray(); PrecisionBox.SelectedIndex = 2;
    }

    private async void OnPaste(object sender, RoutedEventArgs e) { var data = Clipboard.GetContent(); if (data.Contains(StandardDataFormats.Text)) InputBox.Text = await data.GetTextAsync(); }
    private async void OnImport(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker(); picker.FileTypeFilter.Add(".csv"); picker.FileTypeFilter.Add(".tsv"); picker.FileTypeFilter.Add(".txt"); WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow));
        var file = await picker.PickSingleFileAsync(); if (file is null) return; InputBox.Text = await Windows.Storage.FileIO.ReadTextAsync(file); ConvertInput(file.Name.EndsWith(".tsv", StringComparison.OrdinalIgnoreCase) ? '\t' : CoordinateBatchConversionService.DetectDelimiter(InputBox.Text));
    }
    private void OnConvert(object sender, RoutedEventArgs e) => ConvertInput(CoordinateBatchConversionService.DetectDelimiter(InputBox.Text));
    private void ConvertInput(char delimiter)
    {
        _rows.Clear(); var order = (CoordinateOrder)((Option)OrderBox.SelectedItem).Value; var format = (CoordinateTextFormat)((Option)FormatBox.SelectedItem).Value; var decimals = (int)((Option)PrecisionBox.SelectedItem).Value;
        var lines = InputBox.Text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries); var parsed = lines.Select(line => (IReadOnlyDictionary<string, string>)new Dictionary<string, string> { ["Coordinate"] = line }).ToArray();
        if (lines.Length > 1 && lines[0].Contains(delimiter)) { var table = CoordinateBatchConversionService.ParseDelimited(InputBox.Text, delimiter); if (table.Count > 0) { var headers = table[0].Keys.ToArray(); var combined = headers.FirstOrDefault(header => header.Contains("coordinate", StringComparison.OrdinalIgnoreCase)); var lon = headers.FirstOrDefault(header => header.Contains("lon", StringComparison.OrdinalIgnoreCase)); var lat = headers.FirstOrDefault(header => header.Contains("lat", StringComparison.OrdinalIgnoreCase)); if (combined is not null || (lon is not null && lat is not null)) { parsed = table.ToArray(); var result = _service.ParseRows(parsed, lon, lat, combined, order); AddRows(result, format, decimals); return; } } }
        AddRows(_service.ParseRows(parsed, null, null, "Coordinate", order), format, decimals);
    }
    private void AddRows(CoordinateBatchResult result, CoordinateTextFormat format, int decimals) { foreach (var row in result.Rows) _rows.Add(new(row, format, decimals)); StatsText.Text = _localization.GetFormattedString("CoordinateBatch_Stats", result.Total, result.SuccessCount, result.WarningCount, result.ErrorCount); }
    private void OnOutputSettingsChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_rows.Count == 0 || FormatBox.SelectedItem is not Option formatOption || PrecisionBox.SelectedItem is not Option precisionOption) return;
        var format = (CoordinateTextFormat)formatOption.Value;
        var decimals = (int)precisionOption.Value;
        foreach (var row in _rows) row.Update(format, decimals);
    }
    private void OnCopy(object sender, RoutedEventArgs e) { if (_rows.Count == 0) return; var text = string.Join(Environment.NewLine, _rows.Select(row => $"{row.Id}\t{row.OriginalText}\t{row.Longitude}\t{row.Latitude}")); var package = new DataPackage(); package.SetText(text); Clipboard.SetContent(package); }
    private async void OnExport(object sender, RoutedEventArgs e) { if (_rows.Count == 0 || FormatBox.SelectedItem is not Option formatOption || PrecisionBox.SelectedItem is not Option precisionOption) return; var format = (CoordinateTextFormat)formatOption.Value; var decimals = (int)precisionOption.Value; var picker = new FileSavePicker(); picker.FileTypeChoices.Add("CSV", [".csv"]); picker.SuggestedFileName = "coordinate-results"; WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow)); var file = await picker.PickSaveFileAsync(); if (file is null) return; await Windows.Storage.FileIO.WriteTextAsync(file, CoordinateBatchConversionService.ExportCsv(_rows.Select(row => row.Source), format, decimals)); }
    private sealed record Option(object Value, string Label);
    private sealed class CoordinateBatchDisplayRow : System.ComponentModel.INotifyPropertyChanged
    {
        public CoordinateBatchDisplayRow(CoordinateBatchRow source, CoordinateTextFormat format, int decimals) { Source = source; Update(format, decimals); }
        public CoordinateBatchRow Source { get; }
        public string Id => Source.Id;
        public string OriginalText => Source.OriginalText;
        public string Longitude { get; private set; } = "";
        public string Latitude { get; private set; } = "";
        public string Format => Source.Result.DetectedFormat.ToString();
        public string Status => Source.Result.Status + (string.IsNullOrWhiteSpace(Source.Result.Message) ? "" : $": {Source.Result.Message}");
        public void Update(CoordinateTextFormat format, int decimals)
        {
            Longitude = Source.Result.Coordinate is null ? "" : CoordinateBatchConversionService.FormatSingle(Source.Result.Coordinate.Longitude, true, format, decimals);
            Latitude = Source.Result.Coordinate is null ? "" : CoordinateBatchConversionService.FormatSingle(Source.Result.Coordinate.Latitude, false, format, decimals);
            PropertyChanged?.Invoke(this, new(nameof(Longitude)));
            PropertyChanged?.Invoke(this, new(nameof(Latitude)));
        }
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}
