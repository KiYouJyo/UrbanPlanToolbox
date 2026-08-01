using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.Services;

public sealed class PlanningCalculationService
{
    private const decimal Tolerance = 0.01m;
    private readonly ILocalizationService _localization;

    public PlanningCalculationService(ILocalizationService localization)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
    }

    public PlanningResult Calculate(PlanningInput input)
    {
        var result = new PlanningResult { TotalBuildingArea = input.TotalBuildingArea, AboveGroundArea = input.AboveGroundArea, UndergroundArea = input.UndergroundArea, TotalParkingSpaces = input.TotalParkingSpaces, Population = input.Population };
        Validate(input, result);
        ResolveBuildingAreas(input, result);
        ResolveParking(input, result);
        if (result.Population is null && Positive(input.HouseholdCount) && Positive(input.PeoplePerHousehold)) result.Population = input.HouseholdCount * input.PeoplePerHousehold;
        if (Positive(input.SiteArea))
        {
            result.FloorAreaRatio = Divide(result.AboveGroundArea, input.SiteArea);
            result.BuildingDensity = Percent(input.BuildingFootprint, input.SiteArea);
            result.GreenRatio = Percent(input.GreenArea, input.SiteArea);
            result.PerCapitaSiteArea = Divide(input.SiteArea, result.Population);
            result.PerHouseholdSiteArea = Divide(input.SiteArea, input.HouseholdCount);
            if (input.BuildingFootprint > input.SiteArea) result.Warnings.Add(_localization.GetString("Warning_FootprintExceedsSite"));
            if (input.GreenArea > input.SiteArea) result.Warnings.Add(_localization.GetString("Warning_GreenAreaExceedsSite"));
        }
        result.ParkingPerHousehold = Divide(result.TotalParkingSpaces, input.HouseholdCount);
        result.ParkingPer100Households = result.ParkingPerHousehold * 100m;
        result.UndergroundAreaRatio = Percent(result.UndergroundArea, result.TotalBuildingArea);
        result.PublicServiceAreaRatio = Percent(input.PublicServiceArea, input.PublicServiceUsesTotalArea ? result.TotalBuildingArea : result.AboveGroundArea);
        result.PublicServiceAreaPerHousehold = Divide(input.PublicServiceArea, input.HouseholdCount);
        result.PublicServiceAreaPerCapita = Divide(input.PublicServiceArea, result.Population);
        if (input.PublicServiceArea is not null && result.TotalBuildingArea is not null && input.PublicServiceArea > result.TotalBuildingArea) result.Warnings.Add(_localization.GetString("Warning_PublicServiceExceedsTotal"));
        return result;
    }

    private void ResolveBuildingAreas(PlanningInput input, PlanningResult result)
    {
        if (result.TotalBuildingArea is null && input.AboveGroundArea is not null && input.UndergroundArea is not null) { result.TotalBuildingArea = input.AboveGroundArea + input.UndergroundArea; result.AutoCalculatedFields.Add("总建筑面积"); }
        else if (result.AboveGroundArea is null && input.TotalBuildingArea is not null && input.UndergroundArea is not null) { result.AboveGroundArea = input.TotalBuildingArea - input.UndergroundArea; result.AutoCalculatedFields.Add("地上建筑面积"); }
        else if (result.UndergroundArea is null && input.TotalBuildingArea is not null && input.AboveGroundArea is not null) { result.UndergroundArea = input.TotalBuildingArea - input.AboveGroundArea; result.AutoCalculatedFields.Add("地下建筑面积"); }
        if (result.TotalBuildingArea is not null && result.AboveGroundArea is not null && result.UndergroundArea is not null && decimal.Abs(result.TotalBuildingArea.Value - result.AboveGroundArea.Value - result.UndergroundArea.Value) > Tolerance) result.Warnings.Add(_localization.GetString("Warning_TotalAreaMismatch"));
        if (result.AboveGroundArea < 0 || result.UndergroundArea < 0) result.Errors.Add(_localization.GetString("Error_DerivedAreaNegative"));
    }

    private void ResolveParking(PlanningInput input, PlanningResult result)
    {
        if (result.TotalParkingSpaces is null && input.SurfaceParkingSpaces is not null && input.UndergroundParkingSpaces is not null) { result.TotalParkingSpaces = input.SurfaceParkingSpaces + input.UndergroundParkingSpaces; result.AutoCalculatedFields.Add("机动车停车位总数"); }
        if (result.TotalParkingSpaces is not null && input.SurfaceParkingSpaces is not null && input.UndergroundParkingSpaces is not null && decimal.Abs(result.TotalParkingSpaces.Value - input.SurfaceParkingSpaces.Value - input.UndergroundParkingSpaces.Value) > Tolerance) result.Warnings.Add(_localization.GetString("Warning_ParkingTotalMismatch"));
    }

    private static bool Positive(decimal? value) => value is > 0;
    private static decimal? Divide(decimal? numerator, decimal? denominator) => numerator is not null && denominator is > 0 ? numerator / denominator : null;
    private static decimal? Percent(decimal? numerator, decimal? denominator) => Divide(numerator, denominator) * 100m;
    private void Validate(PlanningInput input, PlanningResult result)
    {
        foreach (var (key, value) in new (string Key, decimal? Value)[] { ("Field_SiteArea", input.SiteArea), ("Field_TotalBuildingArea", input.TotalBuildingArea), ("Field_AboveGroundArea", input.AboveGroundArea), ("Field_UndergroundArea", input.UndergroundArea), ("Field_BuildingFootprint", input.BuildingFootprint), ("Field_GreenArea", input.GreenArea), ("Field_HouseholdCount", input.HouseholdCount), ("Field_Population", input.Population), ("Field_PeoplePerHousehold", input.PeoplePerHousehold), ("Field_ParkingSpacesTotal", input.TotalParkingSpaces), ("Field_PublicServiceArea", input.PublicServiceArea) }) if (value < 0) result.Errors.Add(_localization.GetFormattedString("Error_NegativeValue", _localization.GetString(key)));
        if (input.SiteArea == 0) result.Errors.Add(_localization.GetString("Error_SiteAreaZero"));
    }
}
