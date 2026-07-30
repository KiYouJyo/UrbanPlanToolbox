using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Services;
using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class PlanningCalculationServiceTests
{
 private readonly PlanningCalculationService _service = new();
 [Fact] public void CalculatesStandardIndicators() { var r = _service.Calculate(new PlanningInput { SiteArea = 50000, AboveGroundArea = 100000, UndergroundArea = 30000, BuildingFootprint = 12500, GreenArea = 17500, HouseholdCount = 800, PeoplePerHousehold = 2.8m, TotalParkingSpaces = 900, PublicServiceArea = 6000 }); Assert.Equal(2m, r.FloorAreaRatio); Assert.Equal(25m, r.BuildingDensity); Assert.Equal(35m, r.GreenRatio); Assert.Equal(2240m, r.Population); Assert.Equal(1.125m, r.ParkingPerHousehold); }
 [Fact] public void RejectsZeroSiteArea() => Assert.Contains("用地面积不能为零。", _service.Calculate(new PlanningInput { SiteArea = 0 }).Errors);
 [Fact] public void EmptyInputDoesNotProduceZeroes() => Assert.Null(_service.Calculate(new PlanningInput()).FloorAreaRatio);
 [Fact] public void DerivesPopulationFromHouseholds() => Assert.Equal(1000m, _service.Calculate(new PlanningInput { HouseholdCount = 400, PeoplePerHousehold = 2.5m }).Population);
 [Fact] public void DerivesTotalArea() { var r = _service.Calculate(new PlanningInput { AboveGroundArea = 10, UndergroundArea = 5 }); Assert.Equal(15m, r.TotalBuildingArea); Assert.Contains("总建筑面积", r.AutoCalculatedFields); }
 [Fact] public void DerivesAboveGroundArea() => Assert.Equal(7m, _service.Calculate(new PlanningInput { TotalBuildingArea = 10, UndergroundArea = 3 }).AboveGroundArea);
 [Fact] public void DerivesUndergroundArea() => Assert.Equal(3m, _service.Calculate(new PlanningInput { TotalBuildingArea = 10, AboveGroundArea = 7 }).UndergroundArea);
 [Fact] public void WarnsOnAreaConflict() => Assert.NotEmpty(_service.Calculate(new PlanningInput { TotalBuildingArea = 20, AboveGroundArea = 10, UndergroundArea = 5 }).Warnings);
 [Fact] public void DerivesParkingTotal() => Assert.Equal(30m, _service.Calculate(new PlanningInput { SurfaceParkingSpaces = 10, UndergroundParkingSpaces = 20 }).TotalParkingSpaces);
 [Fact] public void WarnsOnParkingConflict() => Assert.NotEmpty(_service.Calculate(new PlanningInput { TotalParkingSpaces = 50, SurfaceParkingSpaces = 10, UndergroundParkingSpaces = 20 }).Warnings);
 [Fact] public void CalculatesPublicServiceAgainstTotalWhenSelected() => Assert.Equal(10m, _service.Calculate(new PlanningInput { TotalBuildingArea = 100, AboveGroundArea = 50, PublicServiceArea = 10, PublicServiceUsesTotalArea = true }).PublicServiceAreaRatio);
 [Fact] public void SupportsDecimalInput() => Assert.Equal(25.5m, _service.Calculate(new PlanningInput { SiteArea = 100, BuildingFootprint = 25.5m }).BuildingDensity);
 [Fact] public void RejectsNegativeValues() => Assert.NotEmpty(_service.Calculate(new PlanningInput { GreenArea = -1 }).Errors);
 [Fact] public void DoesNotCalculatePerCapitaWithoutPopulation() => Assert.Null(_service.Calculate(new PlanningInput { SiteArea = 100, HouseholdCount = 2 }).PerCapitaSiteArea);
 [Fact] public void DoesNotCalculatePerHouseholdWhenZero() => Assert.Null(_service.Calculate(new PlanningInput { SiteArea = 100, HouseholdCount = 0 }).PerHouseholdSiteArea);
}
