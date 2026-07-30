using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.Helpers;

public static class PlanningResultFormatter
{
    public static string Format(PlanningResult result, int decimalPlaces) => string.Join(Environment.NewLine, new[]
    {
        $"容积率：{NumberFormatter.Value(result.FloorAreaRatio, decimalPlaces)}",
        $"建筑密度：{NumberFormatter.Percent(result.BuildingDensity, decimalPlaces)}",
        $"绿地率：{NumberFormatter.Percent(result.GreenRatio, decimalPlaces)}",
        $"规划人口：{NumberFormatter.Value(result.Population, 0, " 人")}",
        $"人均用地面积：{NumberFormatter.Value(result.PerCapitaSiteArea, decimalPlaces, " ㎡/人")}",
        $"户均用地面积：{NumberFormatter.Value(result.PerHouseholdSiteArea, decimalPlaces, " ㎡/户")}",
        $"户均停车位：{NumberFormatter.Value(result.ParkingPerHousehold, decimalPlaces, " 个/户")}",
        $"每100户停车位：{NumberFormatter.Value(result.ParkingPer100Households, decimalPlaces, " 个")}",
        $"总建筑面积：{NumberFormatter.Value(result.TotalBuildingArea, decimalPlaces, " ㎡")}",
        $"地上建筑面积：{NumberFormatter.Value(result.AboveGroundArea, decimalPlaces, " ㎡")}",
        $"地下建筑面积：{NumberFormatter.Value(result.UndergroundArea, decimalPlaces, " ㎡")}",
        $"地下建筑面积占比：{NumberFormatter.Percent(result.UndergroundAreaRatio, decimalPlaces)}",
        $"公共服务设施面积占比：{NumberFormatter.Percent(result.PublicServiceAreaRatio, decimalPlaces)}",
        $"户均公共服务设施面积：{NumberFormatter.Value(result.PublicServiceAreaPerHousehold, decimalPlaces, " ㎡/户")}",
        $"人均公共服务设施面积：{NumberFormatter.Value(result.PublicServiceAreaPerCapita, decimalPlaces, " ㎡/人")}" });
}
