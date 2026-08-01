using System.Globalization;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Windows.ApplicationModel.DataTransfer;
using UrbanPlanToolbox.Helpers;
using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Models.Tools;
using UrbanPlanToolbox.Services;

namespace UrbanPlanToolbox.Views;

public sealed partial class PlanningCalculatorPage : Page
{
    private readonly PlanningCalculationService _calculator;
    private readonly SettingsService _settingsService = new();
    private readonly ILocalizationService _localization = LocalizationService.Default;
    private readonly DispatcherQueueTimer _autoCalculateTimer;
    private PlanningResult? _lastResult;
    private PlanningInput? _lastInput;
    private Guid? _projectId;
    private bool _isProgrammaticChange;

    public PlanningCalculatorPage()
    {
        InitializeComponent();
        _calculator = new PlanningCalculationService(_localization);
        FavoriteButton.ToolId = ToolIds.PlanningIndicatorCalculator;
        TitleText.Text = _localization.GetString("Tool_PlanningIndicator_Name");
        SiteAreaBox.Header = _localization.GetString("Field_SiteArea");
        FootprintBox.Header = _localization.GetString("Field_BuildingFootprint");
        TotalAreaBox.Header = _localization.GetString("Field_TotalBuildingArea");
        AboveAreaBox.Header = _localization.GetString("Field_AboveGroundArea");
        UndergroundAreaBox.Header = _localization.GetString("Field_UndergroundArea");
        GreenAreaBox.Header = _localization.GetString("Field_GreenArea");
        HouseholdsBox.Header = _localization.GetString("Field_HouseholdCount");
        PopulationBox.Header = _localization.GetString("Field_Population");
        PeoplePerHouseholdBox.Header = _localization.GetString("Field_PeoplePerHousehold");
        ParkingTotalBox.Header = _localization.GetString("Field_ParkingSpacesTotal");
        SurfaceParkingBox.Header = _localization.GetString("Field_SurfaceParking");
        UndergroundParkingBox.Header = _localization.GetString("Field_UndergroundParking");
        PublicServiceBox.Header = _localization.GetString("Field_PublicServiceArea");
        ResultsText.Text = _localization.GetString("Result_NotCalculated");
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
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _projectId = e.Parameter as Guid?;
        ProjectActions.Visibility = _projectId.HasValue ? Visibility.Visible : Visibility.Collapsed;
    }
    private void OnLoaded(object sender, RoutedEventArgs e) => SettingsService.SettingsChanged += OnSettingsChanged;
    private void OnUnloaded(object sender, RoutedEventArgs e) => SettingsService.SettingsChanged -= OnSettingsChanged;
    private void OnSettingsChanged(object? sender, AppSettings settings)
    {
        if (_lastResult is not null) ResultsText.Text = PlanningResultFormatter.Format(_lastResult, settings.DecimalPlaces, _localization);
        if (settings.AutoCalculate) ScheduleAutomaticCalculation(); else _autoCalculateTimer.Stop();
    }
    private void OnInputTextChanged(object sender, TextChangedEventArgs e) => HandleInputChanged();
    private void OnInputOptionChanged(object sender, RoutedEventArgs e) => HandleInputChanged();
    private void HandleInputChanged()
    {
        if (_isProgrammaticChange) return;
        StaleBar.Message = _localization.GetString("Status_ResultStaleMessage");
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
        decimal? Parse(TextBox box)
        {
            if (string.IsNullOrWhiteSpace(box.Text)) return null;
            if (decimal.TryParse(box.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var value)) return value;
            invalid.Add(_localization.GetFormattedString("Error_InvalidNumber", box.Header?.ToString() ?? string.Empty));
            return null;
        }
        var input = new PlanningInput { SiteArea = Parse(SiteAreaBox), TotalBuildingArea = Parse(TotalAreaBox), AboveGroundArea = Parse(AboveAreaBox), UndergroundArea = Parse(UndergroundAreaBox), BuildingFootprint = Parse(FootprintBox), GreenArea = Parse(GreenAreaBox), HouseholdCount = Parse(HouseholdsBox), Population = Parse(PopulationBox), PeoplePerHousehold = Parse(PeoplePerHouseholdBox), TotalParkingSpaces = Parse(ParkingTotalBox), SurfaceParkingSpaces = Parse(SurfaceParkingBox), UndergroundParkingSpaces = Parse(UndergroundParkingBox), PublicServiceArea = Parse(PublicServiceBox), PublicServiceUsesTotalArea = UseTotalAreaCheck.IsChecked == true };
        var result = _calculator.Calculate(input);
        if (!showValidation && invalid.Count > 0) { StaleBar.Message = _localization.GetString("Status_InputIncomplete"); StaleBar.IsOpen = true; return; }
        result.Errors.InsertRange(0, invalid);
        ErrorBar.Message = string.Join(Environment.NewLine, result.Errors); ErrorBar.IsOpen = result.Errors.Count > 0;
        WarningBar.Message = string.Join(Environment.NewLine, result.Warnings); WarningBar.IsOpen = result.Warnings.Count > 0;
        _lastInput = input; _lastResult = result; ResultsText.Text = PlanningResultFormatter.Format(result, CurrentSettings.DecimalPlaces, _localization); StaleBar.IsOpen = false;
    }
    private void OnSample(object sender, RoutedEventArgs e)
    {
        _isProgrammaticChange = true; SiteAreaBox.Text = "50000"; AboveAreaBox.Text = "100000"; UndergroundAreaBox.Text = "30000"; FootprintBox.Text = "12500"; GreenAreaBox.Text = "17500"; HouseholdsBox.Text = "800"; PeoplePerHouseholdBox.Text = "2.8"; ParkingTotalBox.Text = "900"; SurfaceParkingBox.Text = "150"; UndergroundParkingBox.Text = "750"; PublicServiceBox.Text = "6000"; _isProgrammaticChange = false; CalculateInternal(showValidation: true);
    }
    private void OnClear(object sender, RoutedEventArgs e)
    {
        _isProgrammaticChange = true; foreach (var box in InputBoxes) box.Text = ""; _isProgrammaticChange = false; ErrorBar.IsOpen = WarningBar.IsOpen = StaleBar.IsOpen = false; _lastInput = null; _lastResult = null; ResultsText.Text = _localization.GetString("Result_NotCalculated");
    }
    private void OnCopy(object sender, RoutedEventArgs e) { var data = new DataPackage(); data.SetText(_localization.GetString("Copy_PlanningResultsHeader") + "\r\n\r\n" + ResultsText.Text); Clipboard.SetContent(data); }
    private async void OnSaveToProject(object sender, RoutedEventArgs e)
    {
        if (!_projectId.HasValue || _lastInput is null || _lastResult is null || _lastResult.Errors.Count > 0)
        {
            ErrorBar.Message = _localization.GetString("Snapshot_Error_ValidResultRequired"); ErrorBar.IsOpen = true; return;
        }
        var result = await ProjectStorageService.Default.AddSnapshotAsync(_projectId.Value, _lastInput, _lastResult);
        if (!result.Succeeded) { ErrorBar.Message = _localization.GetString("Snapshot_Error_SaveFailed"); ErrorBar.IsOpen = true; return; }
        StaleBar.Message = _localization.GetString("Snapshot_Status_Saved"); StaleBar.IsOpen = true;
        Frame.Navigate(typeof(ProjectWorkspacePage), _projectId.Value);
    }
    private void OnReturnToProject(object sender, RoutedEventArgs e) { if (_projectId.HasValue) Frame.Navigate(typeof(ProjectWorkspacePage), _projectId.Value); }
}
