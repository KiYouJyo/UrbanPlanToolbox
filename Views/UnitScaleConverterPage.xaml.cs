using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using UrbanPlanToolbox.Helpers;
using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Models.Tools;
using UrbanPlanToolbox.Services;

namespace UrbanPlanToolbox.Views;

public sealed partial class UnitScaleConverterPage : Page
{
    private readonly UnitConversionService _unitService = new();
    private readonly ScaleConversionService _scaleService = new();
    private readonly SettingsService _settingsService = new();
    private MeasurementCategory _category = MeasurementCategory.Length;

    public UnitScaleConverterPage()
    {
        InitializeComponent();
        FavoriteButton.ToolId = ToolIds.UnitScaleConverter;
        DrawingUnitBox.SelectedIndex = ActualUnitBox.SelectedIndex = ActualInputUnitBox.SelectedIndex = DrawingOutputUnitBox.SelectedIndex = 0;
        LoadUnits();
        Loaded += (_, _) => SettingsService.SettingsChanged += OnSettingsChanged;
        Unloaded += (_, _) => SettingsService.SettingsChanged -= OnSettingsChanged;
    }

    private int DecimalPlaces => _settingsService.Load().DecimalPlaces;
    private void OnSettingsChanged(object? sender, AppSettings settings)
    {
        if (UnitResultText.Tag is ConversionResult unit && TargetUnitBox.SelectedItem is MeasurementUnit target) UnitResultText.Text = ConversionResultFormatter.Format(unit, settings.DecimalPlaces, target.Symbol);
        RefreshScaleResult(ForwardResultText, ActualUnitBox, settings.DecimalPlaces);
        RefreshScaleResult(ReverseResultText, DrawingOutputUnitBox, settings.DecimalPlaces);
    }

    private static void RefreshScaleResult(TextBlock resultText, ComboBox unitBox, int decimalPlaces)
    {
        if (resultText.Tag is ConversionResult result) resultText.Text = ConversionResultFormatter.Format(result, decimalPlaces, (unitBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "");
    }
    private void OnCategoryChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ConverterPivot.SelectedItem is PivotItem { Tag: string tag } && Enum.TryParse(tag, out MeasurementCategory category)) { _category = category; UnitPanel.Visibility = Visibility.Visible; LoadUnits(); }
        else UnitPanel.Visibility = Visibility.Collapsed;
    }
    private void LoadUnits()
    {
        var units = _unitService.GetUnits(_category);
        SourceUnitBox.ItemsSource = units; TargetUnitBox.ItemsSource = units;
        SourceUnitBox.SelectedItem = units.First(); TargetUnitBox.SelectedItem = units.Skip(2).FirstOrDefault() ?? units.Last();
        UnitValueBox.Text = ""; UnitResultText.Text = "请输入数值并选择单位。"; UnitResultText.Tag = null; UnitErrorBar.IsOpen = false;
        UnitNoteText.Text = _category == MeasurementCategory.Area ? "亩、坪、反和町属于地区性或传统单位。日本“畳/帖”面积标准存在差异，本版本不提供固定换算。" : _category == MeasurementCategory.Length ? "日本传统建筑单位：尺（しゃく）与间（けん）。请勿与中国市尺混用。" : "此处体积指建筑空间、土方、混凝土等几何体积；不提供 gallon、fluid ounce、pint、quart 或 cup。";
    }
    private static decimal? Parse(TextBox box, InfoBar errorBar)
    {
        if (string.IsNullOrWhiteSpace(box.Text)) return null;
        if (decimal.TryParse(box.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var value)) return value;
        errorBar.Message = "请输入合法数字。"; errorBar.IsOpen = true; return null;
    }
    private void OnUnitCalculate(object sender, RoutedEventArgs e)
    {
        UnitErrorBar.IsOpen = false;
        var value = Parse(UnitValueBox, UnitErrorBar); if (value is null) { if (string.IsNullOrWhiteSpace(UnitValueBox.Text)) UnitResultText.Text = "请输入数值。"; return; }
        var result = _unitService.Convert(value.Value, (SourceUnitBox.SelectedItem as MeasurementUnit)?.Id, (TargetUnitBox.SelectedItem as MeasurementUnit)?.Id);
        UnitErrorBar.Message = result.Error ?? ""; UnitErrorBar.IsOpen = !result.IsSuccess;
        UnitResultText.Tag = result; UnitResultText.Text = ConversionResultFormatter.Format(result, DecimalPlaces, (TargetUnitBox.SelectedItem as MeasurementUnit)?.Symbol ?? "");
    }
    private void OnSwapUnits(object sender, RoutedEventArgs e)
    {
        (SourceUnitBox.SelectedItem, TargetUnitBox.SelectedItem) = (TargetUnitBox.SelectedItem, SourceUnitBox.SelectedItem);
        if (!string.IsNullOrWhiteSpace(UnitValueBox.Text)) OnUnitCalculate(sender, e);
    }
    private void OnUnitSample(object sender, RoutedEventArgs e)
    {
        var (value, source, target) = _category switch { MeasurementCategory.Length => ("10", "length-m", "length-ft"), MeasurementCategory.Area => ("100", "area-m2", "area-tsubo"), _ => ("1", "volume-m3", "volume-ft3") };
        UnitValueBox.Text = value; SourceUnitBox.SelectedItem = UnitConversionService.Units.Single(unit => unit.Id == source); TargetUnitBox.SelectedItem = UnitConversionService.Units.Single(unit => unit.Id == target); OnUnitCalculate(sender, e);
    }
    private void OnUnitClear(object sender, RoutedEventArgs e) { UnitValueBox.Text = ""; UnitResultText.Text = "请输入数值并选择单位。"; UnitResultText.Tag = null; UnitErrorBar.IsOpen = false; }
    private void OnUnitCopy(object sender, RoutedEventArgs e) => CopyResult(UnitResultText, UnitErrorBar);
    private void OnForwardCalculate(object sender, RoutedEventArgs e) => CalculateScale(ForwardDenominatorBox, DrawingLengthBox, DrawingUnitBox, ActualUnitBox, true, ForwardErrorBar, ForwardResultText);
    private void OnReverseCalculate(object sender, RoutedEventArgs e) => CalculateScale(ReverseDenominatorBox, ActualLengthBox, ActualInputUnitBox, DrawingOutputUnitBox, false, ReverseErrorBar, ReverseResultText);
    private void CalculateScale(TextBox denominatorBox, TextBox lengthBox, ComboBox sourceBox, ComboBox targetBox, bool forward, InfoBar errorBar, TextBlock resultText)
    {
        errorBar.IsOpen = false;
        var denominator = Parse(denominatorBox, errorBar); var length = Parse(lengthBox, errorBar); if (denominator is null || length is null) { if (string.IsNullOrWhiteSpace(denominatorBox.Text) || string.IsNullOrWhiteSpace(lengthBox.Text)) resultText.Text = "请输入比例尺分母和长度。"; return; }
        var source = (sourceBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? ""; var target = (targetBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";
        var result = forward ? _scaleService.DrawingToActual(denominator.Value, length.Value, source, target) : _scaleService.ActualToDrawing(denominator.Value, length.Value, source, target);
        errorBar.Message = result.Error ?? ""; errorBar.IsOpen = !result.IsSuccess; resultText.Tag = result; resultText.Text = ConversionResultFormatter.Format(result, DecimalPlaces, target);
    }
    private void OnForwardSample(object sender, RoutedEventArgs e) { ForwardDenominatorBox.Text = "1000"; DrawingLengthBox.Text = "25"; DrawingUnitBox.SelectedIndex = ActualUnitBox.SelectedIndex = 0; OnForwardCalculate(sender, e); }
    private void OnReverseSample(object sender, RoutedEventArgs e) { ReverseDenominatorBox.Text = "500"; ActualLengthBox.Text = "30"; ActualInputUnitBox.SelectedIndex = DrawingOutputUnitBox.SelectedIndex = 0; OnReverseCalculate(sender, e); }
    private void OnForwardClear(object sender, RoutedEventArgs e) { ForwardDenominatorBox.Text = DrawingLengthBox.Text = ""; ForwardResultText.Text = "请输入比例尺和图上长度。"; ForwardResultText.Tag = null; ForwardErrorBar.IsOpen = false; }
    private void OnReverseClear(object sender, RoutedEventArgs e) { ReverseDenominatorBox.Text = ActualLengthBox.Text = ""; ReverseResultText.Text = "请输入比例尺和实际长度。"; ReverseResultText.Tag = null; ReverseErrorBar.IsOpen = false; }
    private void OnForwardCopy(object sender, RoutedEventArgs e) => CopyResult(ForwardResultText, ForwardErrorBar);
    private void OnReverseCopy(object sender, RoutedEventArgs e) => CopyResult(ReverseResultText, ReverseErrorBar);
    private static void CopyResult(TextBlock resultText, InfoBar errorBar)
    {
        if (resultText.Tag is not ConversionResult { IsSuccess: true }) { errorBar.Message = "没有可复制的有效结果。"; errorBar.IsOpen = true; return; }
        Copy(resultText.Text);
    }
    private static void Copy(string value) { var data = new DataPackage(); data.SetText(value); Clipboard.SetContent(data); }
}
