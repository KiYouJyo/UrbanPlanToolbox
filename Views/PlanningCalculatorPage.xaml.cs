using System.Globalization;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using UrbanPlanToolbox.Helpers;
using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Models.Tools;
using UrbanPlanToolbox.Services;

namespace UrbanPlanToolbox.Views;

public sealed partial class PlanningCalculatorPage : Page
{
    private readonly PlanningCalculationService _calculator = new();
    private readonly SettingsService _settingsService = new();
    private readonly DispatcherQueueTimer _autoCalculateTimer;
    private PlanningResult? _lastResult;
    private bool _isProgrammaticChange;

    public PlanningCalculatorPage()
    {
        InitializeComponent();
        FavoriteButton.ToolId = ToolIds.PlanningIndicatorCalculator;
        _autoCalculateTimer = DispatcherQueue.CreateTimer();
        _autoCalculateTimer.Interval = TimeSpan.FromMilliseconds(350);
        _autoCalculateTimer.Tick += (_, _) => { _autoCalculateTimer.Stop(); CalculateInternal(showValidation: false); };
        foreach (var box in InputBoxes) box.TextChanged += OnInputTextChanged;
        UseTotalAreaCheck.Checked += OnInputOptionChanged;
        UseTotalAreaCheck.Unchecked += OnInputOptionChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private IEnumerable<TextBox> InputBoxes => new[] { SiteAreaBox, FootprintBox, TotalAreaBox, AboveAreaBox, UndergroundAreaBox, GreenAreaBox, HouseholdsBox, PopulationBox, PeoplePerHouseholdBox, ParkingTotalBox, SurfaceParkingBox, UndergroundParkingBox, PublicServiceBox };
    private AppSettings CurrentSettings => _settingsService.Load();
    private void OnLoaded(object sender, RoutedEventArgs e) => SettingsService.SettingsChanged += OnSettingsChanged;
    private void OnUnloaded(object sender, RoutedEventArgs e) => SettingsService.SettingsChanged -= OnSettingsChanged;
    private void OnSettingsChanged(object? sender, AppSettings settings)
    {
        if (_lastResult is not null) ResultsText.Text = PlanningResultFormatter.Format(_lastResult, settings.DecimalPlaces);
        if (settings.AutoCalculate) ScheduleAutomaticCalculation(); else _autoCalculateTimer.Stop();
    }
    private void OnInputTextChanged(object sender, TextChangedEventArgs e) => HandleInputChanged();
    private void OnInputOptionChanged(object sender, RoutedEventArgs e) => HandleInputChanged();
    private void HandleInputChanged()
    {
        if (_isProgrammaticChange) return;
        StaleBar.IsOpen = _lastResult is not null;
        if (CurrentSettings.AutoCalculate) ScheduleAutomaticCalculation();
    }
    private void ScheduleAutomaticCalculation()
    {
        _autoCalculateTimer.Stop();
        _autoCalculateTimer.Start();
    }
    private void OnCalculate(object sender, RoutedEventArgs e) => CalculateInternal(showValidation: true);
    private void CalculateInternal(bool showValidation)
    {
        var invalid = new List<string>();
        decimal? Parse(TextBox box, string name)
        {
            if (string.IsNullOrWhiteSpace(box.Text)) return null;
            if (decimal.TryParse(box.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var value)) return value;
            invalid.Add($"{name}必须是合法数字。");
            return null;
        }
        var result = _calculator.Calculate(new PlanningInput { SiteArea = Parse(SiteAreaBox, "用地面积"), TotalBuildingArea = Parse(TotalAreaBox, "总建筑面积"), AboveGroundArea = Parse(AboveAreaBox, "地上建筑面积"), UndergroundArea = Parse(UndergroundAreaBox, "地下建筑面积"), BuildingFootprint = Parse(FootprintBox, "建筑基底面积"), GreenArea = Parse(GreenAreaBox, "绿地面积"), HouseholdCount = Parse(HouseholdsBox, "户数"), Population = Parse(PopulationBox, "规划人口"), PeoplePerHousehold = Parse(PeoplePerHouseholdBox, "户均人口"), TotalParkingSpaces = Parse(ParkingTotalBox, "停车位总数"), SurfaceParkingSpaces = Parse(SurfaceParkingBox, "地上停车位"), UndergroundParkingSpaces = Parse(UndergroundParkingBox, "地下停车位"), PublicServiceArea = Parse(PublicServiceBox, "公共服务设施面积"), PublicServiceUsesTotalArea = UseTotalAreaCheck.IsChecked == true });
        if (!showValidation && invalid.Count > 0) { StaleBar.Message = "输入尚不完整，当前结果未更新。"; StaleBar.IsOpen = true; return; }
        result.Errors.InsertRange(0, invalid);
        ErrorBar.Message = string.Join(Environment.NewLine, result.Errors); ErrorBar.IsOpen = result.Errors.Count > 0;
        WarningBar.Message = string.Join(Environment.NewLine, result.Warnings); WarningBar.IsOpen = result.Warnings.Count > 0;
        _lastResult = result; ResultsText.Text = PlanningResultFormatter.Format(result, CurrentSettings.DecimalPlaces); StaleBar.IsOpen = false;
    }
    private void OnSample(object sender, RoutedEventArgs e)
    {
        _isProgrammaticChange = true; SiteAreaBox.Text = "50000"; AboveAreaBox.Text = "100000"; UndergroundAreaBox.Text = "30000"; FootprintBox.Text = "12500"; GreenAreaBox.Text = "17500"; HouseholdsBox.Text = "800"; PeoplePerHouseholdBox.Text = "2.8"; ParkingTotalBox.Text = "900"; SurfaceParkingBox.Text = "150"; UndergroundParkingBox.Text = "750"; PublicServiceBox.Text = "6000"; _isProgrammaticChange = false; CalculateInternal(showValidation: true);
    }
    private void OnClear(object sender, RoutedEventArgs e)
    {
        _isProgrammaticChange = true; foreach (var box in InputBoxes) box.Text = ""; _isProgrammaticChange = false; ErrorBar.IsOpen = WarningBar.IsOpen = StaleBar.IsOpen = false; _lastResult = null; ResultsText.Text = "尚未计算";
    }
    private void OnCopy(object sender, RoutedEventArgs e) { var data = new DataPackage(); data.SetText("规划指标计算结果\r\n\r\n" + ResultsText.Text); Clipboard.SetContent(data); }
}
