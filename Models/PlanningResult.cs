namespace UrbanPlanToolbox.Models;

public sealed class PlanningResult
{
    public decimal? TotalBuildingArea { get; set; }
    public decimal? AboveGroundArea { get; set; }
    public decimal? UndergroundArea { get; set; }
    public decimal? TotalParkingSpaces { get; set; }
    public decimal? Population { get; set; }
    public decimal? FloorAreaRatio { get; set; }
    public decimal? BuildingDensity { get; set; }
    public decimal? GreenRatio { get; set; }
    public decimal? PerCapitaSiteArea { get; set; }
    public decimal? PerHouseholdSiteArea { get; set; }
    public decimal? ParkingPerHousehold { get; set; }
    public decimal? ParkingPer100Households { get; set; }
    public decimal? UndergroundAreaRatio { get; set; }
    public decimal? PublicServiceAreaRatio { get; set; }
    public decimal? PublicServiceAreaPerHousehold { get; set; }
    public decimal? PublicServiceAreaPerCapita { get; set; }
    public List<string> Errors { get; } = [];
    public List<string> Warnings { get; } = [];
    public HashSet<string> AutoCalculatedFields { get; } = [];
}
