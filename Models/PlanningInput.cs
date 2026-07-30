namespace UrbanPlanToolbox.Models;

public sealed class PlanningInput
{
    public decimal? SiteArea { get; init; }
    public decimal? TotalBuildingArea { get; init; }
    public decimal? AboveGroundArea { get; init; }
    public decimal? UndergroundArea { get; init; }
    public decimal? BuildingFootprint { get; init; }
    public decimal? GreenArea { get; init; }
    public decimal? HouseholdCount { get; init; }
    public decimal? Population { get; init; }
    public decimal? PeoplePerHousehold { get; init; }
    public decimal? TotalParkingSpaces { get; init; }
    public decimal? SurfaceParkingSpaces { get; init; }
    public decimal? UndergroundParkingSpaces { get; init; }
    public decimal? PublicServiceArea { get; init; }
    public bool PublicServiceUsesTotalArea { get; init; }
}
