using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.Services;

public sealed class PlanningCalculationService
{
    private const decimal Tolerance = 0.01m;

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
            if (input.BuildingFootprint > input.SiteArea) result.Warnings.Add("建筑基底面积大于用地面积，请确认输入数据。");
            if (input.GreenArea > input.SiteArea) result.Warnings.Add("绿地面积大于用地面积，请确认输入数据。");
        }
        result.ParkingPerHousehold = Divide(result.TotalParkingSpaces, input.HouseholdCount);
        result.ParkingPer100Households = result.ParkingPerHousehold * 100m;
        result.UndergroundAreaRatio = Percent(result.UndergroundArea, result.TotalBuildingArea);
        result.PublicServiceAreaRatio = Percent(input.PublicServiceArea, input.PublicServiceUsesTotalArea ? result.TotalBuildingArea : result.AboveGroundArea);
        result.PublicServiceAreaPerHousehold = Divide(input.PublicServiceArea, input.HouseholdCount);
        result.PublicServiceAreaPerCapita = Divide(input.PublicServiceArea, result.Population);
        if (input.PublicServiceArea is not null && result.TotalBuildingArea is not null && input.PublicServiceArea > result.TotalBuildingArea) result.Warnings.Add("公共服务设施建筑面积大于总建筑面积，请确认输入数据。");
        return result;
    }

    private static void ResolveBuildingAreas(PlanningInput input, PlanningResult result)
    {
        if (result.TotalBuildingArea is null && input.AboveGroundArea is not null && input.UndergroundArea is not null) { result.TotalBuildingArea = input.AboveGroundArea + input.UndergroundArea; result.AutoCalculatedFields.Add("总建筑面积"); }
        else if (result.AboveGroundArea is null && input.TotalBuildingArea is not null && input.UndergroundArea is not null) { result.AboveGroundArea = input.TotalBuildingArea - input.UndergroundArea; result.AutoCalculatedFields.Add("地上建筑面积"); }
        else if (result.UndergroundArea is null && input.TotalBuildingArea is not null && input.AboveGroundArea is not null) { result.UndergroundArea = input.TotalBuildingArea - input.AboveGroundArea; result.AutoCalculatedFields.Add("地下建筑面积"); }
        if (result.TotalBuildingArea is not null && result.AboveGroundArea is not null && result.UndergroundArea is not null && decimal.Abs(result.TotalBuildingArea.Value - result.AboveGroundArea.Value - result.UndergroundArea.Value) > Tolerance) result.Warnings.Add("总建筑面积与地上、地下建筑面积之和不一致，未覆盖您的输入。");
        if (result.AboveGroundArea < 0 || result.UndergroundArea < 0) result.Errors.Add("推算出的建筑面积不能为负数。");
    }

    private static void ResolveParking(PlanningInput input, PlanningResult result)
    {
        if (result.TotalParkingSpaces is null && input.SurfaceParkingSpaces is not null && input.UndergroundParkingSpaces is not null) { result.TotalParkingSpaces = input.SurfaceParkingSpaces + input.UndergroundParkingSpaces; result.AutoCalculatedFields.Add("机动车停车位总数"); }
        if (result.TotalParkingSpaces is not null && input.SurfaceParkingSpaces is not null && input.UndergroundParkingSpaces is not null && decimal.Abs(result.TotalParkingSpaces.Value - input.SurfaceParkingSpaces.Value - input.UndergroundParkingSpaces.Value) > Tolerance) result.Warnings.Add("停车位总数与地上、地下停车位之和不一致，未覆盖您的输入。");
    }

    private static bool Positive(decimal? value) => value is > 0;
    private static decimal? Divide(decimal? numerator, decimal? denominator) => numerator is not null && denominator is > 0 ? numerator / denominator : null;
    private static decimal? Percent(decimal? numerator, decimal? denominator) => Divide(numerator, denominator) * 100m;
    private static void Validate(PlanningInput input, PlanningResult result)
    {
        foreach (var (name, value) in new (string Name, decimal? Value)[] { ("用地面积", input.SiteArea), ("总建筑面积", input.TotalBuildingArea), ("地上建筑面积", input.AboveGroundArea), ("地下建筑面积", input.UndergroundArea), ("建筑基底面积", input.BuildingFootprint), ("绿地面积", input.GreenArea), ("户数", input.HouseholdCount), ("规划人口", input.Population), ("户均人口", input.PeoplePerHousehold), ("停车位", input.TotalParkingSpaces), ("公共服务设施面积", input.PublicServiceArea) }) if (value < 0) result.Errors.Add($"{name}不能为负数。");
        if (input.SiteArea == 0) result.Errors.Add("用地面积不能为零。");
    }
}
