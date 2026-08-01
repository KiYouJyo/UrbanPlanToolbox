using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Services;

namespace UrbanPlanToolbox.Helpers;

public static class PlanningResultFormatter
{
    public static string Format(PlanningResult result, int decimalPlaces, ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(localization);
        return string.Join(Environment.NewLine, new[]
        {
            localization.GetFormattedString("Result_FloorAreaRatio", NumberFormatter.Value(result.FloorAreaRatio, decimalPlaces)),
            localization.GetFormattedString("Result_BuildingDensity", NumberFormatter.Percent(result.BuildingDensity, decimalPlaces)),
            localization.GetFormattedString("Result_GreenRatio", NumberFormatter.Percent(result.GreenRatio, decimalPlaces)),
            localization.GetFormattedString("Result_Population", NumberFormatter.Value(result.Population, 0)),
            localization.GetFormattedString("Result_PerCapitaSiteArea", NumberFormatter.Value(result.PerCapitaSiteArea, decimalPlaces)),
            localization.GetFormattedString("Result_PerHouseholdSiteArea", NumberFormatter.Value(result.PerHouseholdSiteArea, decimalPlaces)),
            localization.GetFormattedString("Result_ParkingPerHousehold", NumberFormatter.Value(result.ParkingPerHousehold, decimalPlaces)),
            localization.GetFormattedString("Result_ParkingPer100Households", NumberFormatter.Value(result.ParkingPer100Households, decimalPlaces)),
            localization.GetFormattedString("Result_TotalBuildingArea", NumberFormatter.Value(result.TotalBuildingArea, decimalPlaces)),
            localization.GetFormattedString("Result_AboveGroundArea", NumberFormatter.Value(result.AboveGroundArea, decimalPlaces)),
            localization.GetFormattedString("Result_UndergroundArea", NumberFormatter.Value(result.UndergroundArea, decimalPlaces)),
            localization.GetFormattedString("Result_UndergroundAreaRatio", NumberFormatter.Percent(result.UndergroundAreaRatio, decimalPlaces)),
            localization.GetFormattedString("Result_PublicServiceAreaRatio", NumberFormatter.Percent(result.PublicServiceAreaRatio, decimalPlaces)),
            localization.GetFormattedString("Result_PublicServiceAreaPerHousehold", NumberFormatter.Value(result.PublicServiceAreaPerHousehold, decimalPlaces)),
            localization.GetFormattedString("Result_PublicServiceAreaPerCapita", NumberFormatter.Value(result.PublicServiceAreaPerCapita, decimalPlaces))
        });
    }
}
