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
    private readonly UnitConversionService _unitService;
    private readonly ScaleConversionService _scaleService;
    private readonly SettingsService _settingsService = new();
    private readonly ILocalizationService _localization = LocalizationService.Default;
    private MeasurementCategory _category = MeasurementCategory.Length;
    private IReadOnlyList<UnitOption> _unitOptions = [];

    public UnitScaleConverterPage()
    {
        InitializeComponent();
        _unitService = new UnitConversionService(_localization);
        _scaleService = new ScaleConversionService(_localization);
        TitleText.Text = _localization.GetString("Tool_UnitScaleConverter_Name");
        ForwardDenominatorBox.Header = _localization.GetString("Field_ScaleDenominator");
        DrawingLengthBox.Header = _localization.GetString("Field_DrawingLength");
        DrawingUnitBox.Header = _localization.GetString("Field_DrawingLengthUnit");
        ActualUnitBox.Header = _localization.GetString("Field_ActualLengthOutputUnit");
        ReverseDenominatorBox.Header = _localization.GetString("Field_ScaleDenominator");
        ActualLengthBox.Header = _localization.GetString("Field_ActualLength");
        ActualInputUnitBox.Header = _localization.GetString("Field_ActualLengthUnit");
        DrawingOutputUnitBox.Header = _localization.GetString("Field_DrawingLengthOutputUnit");
        UnitValueBox.Header = _localization.GetString("Field_InputValue");
        SourceUnitBox.Header = _localization.GetString("Field_SourceUnit");
        TargetUnitBox.Header = _localization.GetString("Field_TargetUnit");
        DrawingUnitBox.SelectedIndex = ActualUnitBox.SelectedIndex = ActualInputUnitBox.SelectedIndex = DrawingOutputUnitBox.SelectedIndex = 0;
        LoadUnits();
        Loaded += (_, _) => SettingsService.SettingsChanged += OnSettingsChanged;
        Unloaded += (_, _) => SettingsService.SettingsChanged -= OnSettingsChanged;
    }

    private int DecimalPlaces => _settingsService.Load().DecimalPlaces;

    private void OnSettingsChanged(object? sender, AppSettings settings)
    {
        if (UnitResultText.Tag is ConversionResult unit && TargetUnitBox.SelectedItem is UnitOption target) UnitResultText.Text = ConversionResultFormatter.Format(unit, settings.DecimalPlaces, target.Symbol);
        RefreshScaleResult(ForwardResultText, ActualUnitBox, settings.DecimalPlaces);
        RefreshScaleResult(ReverseResultText, DrawingOutputUnitBox, settings.DecimalPlaces);
    }

    private static void RefreshScaleResult(TextBlock resultText, ComboBox unitBox, int decimalPlaces)
    {
        if (resultText.Tag is ConversionResult result) resultText.Text = ConversionResultFormatter.Format(result, decimalPlaces, (unitBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "");
    }

    private void OnCategoryChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ConverterPivot.SelectedItem is PivotItem { Tag: string tag } && Enum.TryParse(tag, out MeasurementCategory category))
        {
            _category = category;
            UnitPanel.Visibility = Visibility.Visible;
            LoadUnits();
        }
        else
        {
            UnitPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void LoadUnits()
    {
        var units = _unitService.GetUnits(_category);
        _unitOptions = units.Select(unit => new UnitOption(unit, _localization.GetString(unit.DisplayNameResourceKey))).ToArray();
        SourceUnitBox.ItemsSource = _unitOptions;
        TargetUnitBox.ItemsSource = _unitOptions;
        SourceUnitBox.SelectedItem = _unitOptions.FirstOrDefault();
        TargetUnitBox.SelectedItem = _unitOptions.Skip(2).FirstOrDefault() ?? _unitOptions.LastOrDefault();
        UnitValueBox.Text = "";
        UnitResultText.Text = _localization.GetString("Unit_ResultPlaceholder");
        UnitResultText.Tag = null;
        UnitErrorBar.IsOpen = false;
        UnitNoteText.Text = _category switch
        {
            MeasurementCategory.Area => _localization.GetString("UnitNote_Area"),
            MeasurementCategory.Length => _localization.GetString("UnitNote_Length"),
            _ => _localization.GetString("UnitNote_Volume")
        };
    }

    private static decimal? Parse(TextBox box, InfoBar errorBar, ILocalizationService localization)
    {
        if (string.IsNullOrWhiteSpace(box.Text)) return null;
        if (decimal.TryParse(box.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var value)) return value;
        errorBar.Message = localization.GetString("Error_EnterValidNumber");
        errorBar.IsOpen = true;
        return null;
    }

    private void OnUnitCalculate(object sender, RoutedEventArgs e)
    {
        UnitErrorBar.IsOpen = false;
        var value = Parse(UnitValueBox, UnitErrorBar, _localization);
        if (value is null)
        {
            if (string.IsNullOrWhiteSpace(UnitValueBox.Text)) UnitResultText.Text = _localization.GetString("Unit_EnterValue");
            return;
        }

        var result = _unitService.Convert(value.Value, (SourceUnitBox.SelectedItem as UnitOption)?.Id, (TargetUnitBox.SelectedItem as UnitOption)?.Id);
        UnitErrorBar.Message = result.Error ?? "";
        UnitErrorBar.IsOpen = !result.IsSuccess;
        UnitResultText.Tag = result;
        UnitResultText.Text = ConversionResultFormatter.Format(result, DecimalPlaces, (TargetUnitBox.SelectedItem as UnitOption)?.Symbol ?? "");
    }

    private void OnSwapUnits(object sender, RoutedEventArgs e)
    {
        (SourceUnitBox.SelectedItem, TargetUnitBox.SelectedItem) = (TargetUnitBox.SelectedItem, SourceUnitBox.SelectedItem);
        if (!string.IsNullOrWhiteSpace(UnitValueBox.Text)) OnUnitCalculate(sender, e);
    }

    private void OnUnitSample(object sender, RoutedEventArgs e)
    {
        var (value, source, target) = _category switch
        {
            MeasurementCategory.Length => ("10", "length-m", "length-ft"),
            MeasurementCategory.Area => ("100", "area-m2", "area-tsubo"),
            _ => ("1", "volume-m3", "volume-ft3")
        };
        UnitValueBox.Text = value;
        SourceUnitBox.SelectedItem = _unitOptions.Single(option => option.Id == source);
        TargetUnitBox.SelectedItem = _unitOptions.Single(option => option.Id == target);
        OnUnitCalculate(sender, e);
    }

    private void OnUnitClear(object sender, RoutedEventArgs e)
    {
        UnitValueBox.Text = "";
        UnitResultText.Text = _localization.GetString("Unit_ResultPlaceholder");
        UnitResultText.Tag = null;
        UnitErrorBar.IsOpen = false;
    }

    private void OnUnitCopy(object sender, RoutedEventArgs e) => CopyResult(UnitResultText, UnitErrorBar);

    private void OnForwardCalculate(object sender, RoutedEventArgs e) => CalculateScale(ForwardDenominatorBox, DrawingLengthBox, DrawingUnitBox, ActualUnitBox, true, ForwardErrorBar, ForwardResultText);

    private void OnReverseCalculate(object sender, RoutedEventArgs e) => CalculateScale(ReverseDenominatorBox, ActualLengthBox, ActualInputUnitBox, DrawingOutputUnitBox, false, ReverseErrorBar, ReverseResultText);

    private void CalculateScale(TextBox denominatorBox, TextBox lengthBox, ComboBox sourceBox, ComboBox targetBox, bool forward, InfoBar errorBar, TextBlock resultText)
    {
        errorBar.IsOpen = false;
        var denominator = Parse(denominatorBox, errorBar, _localization);
        var length = Parse(lengthBox, errorBar, _localization);
        if (denominator is null || length is null)
        {
            if (string.IsNullOrWhiteSpace(denominatorBox.Text) || string.IsNullOrWhiteSpace(lengthBox.Text)) resultText.Text = _localization.GetString("Error_EnterScaleAndLength");
            return;
        }

        var source = (sourceBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";
        var target = (targetBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";
        var result = forward
            ? _scaleService.DrawingToActual(denominator.Value, length.Value, source, target)
            : _scaleService.ActualToDrawing(denominator.Value, length.Value, source, target);
        errorBar.Message = result.Error ?? "";
        errorBar.IsOpen = !result.IsSuccess;
        resultText.Tag = result;
        resultText.Text = ConversionResultFormatter.Format(result, DecimalPlaces, target);
    }

    private void OnForwardSample(object sender, RoutedEventArgs e)
    {
        ForwardDenominatorBox.Text = "1000";
        DrawingLengthBox.Text = "25";
        DrawingUnitBox.SelectedIndex = ActualUnitBox.SelectedIndex = 0;
        OnForwardCalculate(sender, e);
    }

    private void OnReverseSample(object sender, RoutedEventArgs e)
    {
        ReverseDenominatorBox.Text = "500";
        ActualLengthBox.Text = "30";
        ActualInputUnitBox.SelectedIndex = DrawingOutputUnitBox.SelectedIndex = 0;
        OnReverseCalculate(sender, e);
    }

    private void OnForwardClear(object sender, RoutedEventArgs e)
    {
        ForwardDenominatorBox.Text = DrawingLengthBox.Text = "";
        ForwardResultText.Text = _localization.GetString("Scale_ResultPlaceholderForward");
        ForwardResultText.Tag = null;
        ForwardErrorBar.IsOpen = false;
    }

    private void OnReverseClear(object sender, RoutedEventArgs e)
    {
        ReverseDenominatorBox.Text = ActualLengthBox.Text = "";
        ReverseResultText.Text = _localization.GetString("Scale_ResultPlaceholderReverse");
        ReverseResultText.Tag = null;
        ReverseErrorBar.IsOpen = false;
    }

    private void OnForwardCopy(object sender, RoutedEventArgs e) => CopyResult(ForwardResultText, ForwardErrorBar);

    private void OnReverseCopy(object sender, RoutedEventArgs e) => CopyResult(ReverseResultText, ReverseErrorBar);

    private void CopyResult(TextBlock resultText, InfoBar errorBar)
    {
        if (resultText.Tag is not ConversionResult { IsSuccess: true })
        {
            errorBar.Message = _localization.GetString("Error_NoCopyableResult");
            errorBar.IsOpen = true;
            return;
        }

        Copy(resultText.Text);
    }

    private static void Copy(string value)
    {
        var data = new DataPackage();
        data.SetText(value);
        Clipboard.SetContent(data);
    }

    private sealed record UnitOption(MeasurementUnit Unit, string DisplayName)
    {
        public string Id => Unit.Id;
        public string Symbol => Unit.Symbol;
    }
}
