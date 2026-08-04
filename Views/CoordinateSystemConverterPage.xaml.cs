using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;
using Windows.System;
using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Services;

namespace UrbanPlanToolbox.Views;

public sealed partial class CoordinateSystemConverterPage : Page
{
    private readonly ILocalizationService _localization = LocalizationService.Default;
    private readonly ICoordinateConversionService _conversion = new CoordinateConversionService();
    private CoordinateConversionResult? _lastResult;
    private readonly IShapefileCoordinateConversionService _shapefileConversion = new ShapefileCoordinateConversionService();
    private CancellationTokenSource? _shapefileCancellation;
    private string? _shapefilePath;
    private string? _outputFolder;

    public CoordinateSystemConverterPage()
    {
        InitializeComponent();
        TitleText.Text = _localization.GetString("Tool_CoordinateSystemConverter_Name");
        SinglePointPivot.Header = _localization.GetString("Coordinate_ModeSingle"); ShapefilePivot.Header = _localization.GetString("Coordinate_ModeShapefile");
        SourceSystemBox.Header = _localization.GetString("Coordinate_Source"); TargetSystemBox.Header = _localization.GetString("Coordinate_Target");
        LongitudeBox.Header = _localization.GetString("Coordinate_Longitude"); LatitudeBox.Header = _localization.GetString("Coordinate_Latitude");
        SwapButton.Content = _localization.GetString("Coordinate_Swap"); ConvertButton.Content = _localization.GetString("Coordinate_ActionConvert"); ClearButton.Content = _localization.GetString("Coordinate_ActionClear"); SampleButton.Content = _localization.GetString("Coordinate_ActionSample");
        CopyLongitudeButton.Content = _localization.GetString("Coordinate_CopyLongitude"); CopyLatitudeButton.Content = _localization.GetString("Coordinate_CopyLatitude"); CopyCoordinateButton.Content = _localization.GetString("Coordinate_CopyCoordinate");
        ShapefileDescriptionText.Text = _localization.GetString("Coordinate_ShapefileDescription"); ShapefilePrivacyText.Text = _localization.GetString("Coordinate_ShapefilePrivacy"); DisclaimerText.Text = _localization.GetString("Coordinate_Disclaimer");
        SourceSystemBox.ItemsSource = TargetSystemBox.ItemsSource = Enum.GetValues<CoordinateSystemType>();
        SourceSystemBox.SelectedItem = CoordinateSystemType.Wgs84; TargetSystemBox.SelectedItem = CoordinateSystemType.Gcj02;
        ShapefileSourceBox.ItemsSource = ShapefileTargetBox.ItemsSource = Enum.GetValues<CoordinateSystemType>();
        ShapefileSourceBox.SelectedItem = CoordinateSystemType.Wgs84; ShapefileTargetBox.SelectedItem = CoordinateSystemType.Gcj02;
        SelectShapefileButton.Content = _localization.GetString("Shapefile_Select"); SelectOutputFolderButton.Content = _localization.GetString("Shapefile_OutputFolder"); ConvertShapefileButton.Content = _localization.GetString("Coordinate_ActionConvert"); CancelShapefileButton.Content = _localization.GetString("Shapefile_Cancel"); OpenOutputFolderButton.Content = _localization.GetString("Shapefile_OpenOutput"); OutputNameBox.Header = _localization.GetString("Shapefile_OutputName");
    }

    private void OnConvert(object sender, RoutedEventArgs e)
    {
        StatusBar.IsOpen = false;
        if (!TryParse(LongitudeBox.Text, out var longitude) || !TryParse(LatitudeBox.Text, out var latitude)) { Show(_localization.GetString("Coordinate_InvalidInput"), InfoBarSeverity.Error); return; }
        var result = _conversion.Convert(new(longitude, latitude), (CoordinateSystemType)SourceSystemBox.SelectedItem, (CoordinateSystemType)TargetSystemBox.SelectedItem);
        _lastResult = result;
        if (!result.IsSuccess) { Show(result.Error ?? _localization.GetString("Coordinate_InvalidInput"), InfoBarSeverity.Error); return; }
        ResultText.Text = $"{result.Point.Longitude:F6}, {result.Point.Latitude:F6}";
        if (result.Warning != CoordinateConversionWarning.None) Show(_localization.GetString(result.Warning == CoordinateConversionWarning.SameCoordinateSystem ? "Coordinate_SameSystem" : "Coordinate_OutsideArea"), InfoBarSeverity.Warning);
    }

    private static bool TryParse(string text, out double value) => double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) && double.IsFinite(value);
    private void OnSwap(object sender, RoutedEventArgs e) => (SourceSystemBox.SelectedItem, TargetSystemBox.SelectedItem) = (TargetSystemBox.SelectedItem, SourceSystemBox.SelectedItem);
    private void OnClear(object sender, RoutedEventArgs e) { LongitudeBox.Text = LatitudeBox.Text = ResultText.Text = string.Empty; _lastResult = null; StatusBar.IsOpen = false; }
    private void OnSample(object sender, RoutedEventArgs e) { LongitudeBox.Text = "116.397128"; LatitudeBox.Text = "39.916527"; OnConvert(sender, e); }
    private void OnCopyLongitude(object sender, RoutedEventArgs e) => Copy(_lastResult?.Point.Longitude.ToString("F6", CultureInfo.InvariantCulture));
    private void OnCopyLatitude(object sender, RoutedEventArgs e) => Copy(_lastResult?.Point.Latitude.ToString("F6", CultureInfo.InvariantCulture));
    private void OnCopyCoordinate(object sender, RoutedEventArgs e) => Copy(ResultText.Text);
    private static void Copy(string? value) { if (!string.IsNullOrWhiteSpace(value)) { var data = new DataPackage(); data.SetText(value); Clipboard.SetContent(data); } }
    private void Show(string message, InfoBarSeverity severity) { StatusBar.Message = message; StatusBar.Severity = severity; StatusBar.IsOpen = true; }

    private async void OnSelectShapefile(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker(); picker.FileTypeFilter.Add(".shp"); WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow));
        var file = await picker.PickSingleFileAsync(); if (file is null) return;
        _shapefilePath = file.Path; var dataset = _shapefileConversion.Inspect(file.Path); SelectedShapefileText.Text = file.Name;
        CompanionStatusText.Text = $".shp: {(dataset.HasShp ? "OK" : "missing")}  .dbf: {(dataset.HasDbf ? "OK" : "missing")}  .shx: {(dataset.HasShx ? "OK" : "missing")}  .prj: {(dataset.HasPrj ? "present" : "optional")}  .cpg: {(dataset.HasCpg ? "present" : "optional")}";
        OutputNameBox.Text = Path.GetFileNameWithoutExtension(file.Name) + "_gcj02"; if (dataset.Warning is not null) Show(dataset.Warning, InfoBarSeverity.Warning);
    }

    private async void OnSelectOutputFolder(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker(); picker.FileTypeFilter.Add("*"); WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow));
        var folder = await picker.PickSingleFolderAsync(); if (folder is not null) { _outputFolder = folder.Path; OutputFolderText.Text = folder.Path; }
    }

    private async void OnConvertShapefile(object sender, RoutedEventArgs e)
    {
        if (_shapefilePath is null || _outputFolder is null) { Show(_localization.GetString("Shapefile_ChooseFirst"), InfoBarSeverity.Warning); return; }
        _shapefileCancellation = new(); ConvertShapefileButton.IsEnabled = false; CancelShapefileButton.IsEnabled = true; ShapefileProgress.Visibility = Visibility.Visible;
        var progress = new Progress<ShapefileConversionProgress>(p => ShapefileProgressText.Text = _localization.GetFormattedString("Shapefile_Progress", p.FeaturesProcessed, p.VerticesProcessed, p.Warnings));
        var result = await _shapefileConversion.ConvertAsync(new(_shapefilePath, _outputFolder, OutputNameBox.Text, (CoordinateSystemType)ShapefileSourceBox.SelectedItem, (CoordinateSystemType)ShapefileTargetBox.SelectedItem), progress, _shapefileCancellation.Token);
        ConvertShapefileButton.IsEnabled = true; CancelShapefileButton.IsEnabled = false; ShapefileProgress.Visibility = Visibility.Collapsed;
        ShapefileProgressText.Text = result.IsSuccess ? $"{_localization.GetString("Shapefile_Completed")}: {result.FeaturesProcessed}, {result.VerticesProcessed}" : result.Error; OpenOutputFolderButton.IsEnabled = result.IsSuccess;
    }

    private void OnCancelShapefile(object sender, RoutedEventArgs e) => _shapefileCancellation?.Cancel();
    private async void OnOpenOutputFolder(object sender, RoutedEventArgs e) { if (_outputFolder is not null) await Launcher.LaunchFolderPathAsync(_outputFolder); }
}
