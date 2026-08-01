using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Services;
using UrbanPlanToolbox.Helpers;
using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class UnitAndScaleConversionTests
{
    private readonly UnitConversionService _units = new(TestLocalization.ZhCn);
    private readonly ScaleConversionService _scale = new(TestLocalization.ZhCn);
    private decimal Convert(decimal value, string source, string target) => Assert.IsType<decimal>(_units.Convert(value, source, target).Value);
    private static void AssertClose(decimal expected, decimal actual) => Assert.InRange(decimal.Abs(expected - actual), 0m, 0.000000000000001m);

    [Fact] public void LengthConvertsMetersToMillimeters() => Assert.Equal(1000m, Convert(1, "length-m", "length-mm"));
    [Fact] public void LengthConvertsInchesExactly() => Assert.Equal(25.4m, Convert(1, "length-in", "length-mm"));
    [Fact] public void LengthConvertsInternationalFootExactly() => Assert.Equal(0.3048m, Convert(1, "length-ft", "length-m"));
    [Fact] public void LengthConvertsYardsToFeet() => Assert.Equal(3m, Convert(1, "length-yd", "length-ft"));
    [Fact] public void LengthConvertsMilesExactly() => Assert.Equal(1609.344m, Convert(1, "length-mi", "length-m"));
    [Fact] public void LengthConvertsJapaneseShakuAsFraction() => Assert.Equal(10m / 33m, Convert(1, "length-shaku", "length-m"));
    [Fact] public void LengthConvertsJapaneseKenToSixShaku() => Assert.Equal(6m, Convert(1, "length-ken", "length-shaku"));
    [Fact] public void LengthRoundTripPreservesValue() => Assert.Equal(12.345m, Convert(Convert(12.345m, "length-m", "length-ft"), "length-ft", "length-m"));
    [Fact] public void SameUnitReturnsInput() => Assert.Equal(42.5m, Convert(42.5m, "length-m", "length-m"));

    [Fact] public void AreaConvertsHectare() => Assert.Equal(1m, Convert(10000, "area-m2", "area-ha"));
    [Fact] public void AreaConvertsSquareKilometersToHectares() => Assert.Equal(100m, Convert(1, "area-km2", "area-ha"));
    [Fact] public void AreaConvertsMuToHectare() => Assert.Equal(1m, Convert(15, "area-mu", "area-ha"));
    [Fact] public void AreaConvertsOneMuAsFraction() => Assert.Equal(2000m / 3m, Convert(1, "area-mu", "area-m2"));
    [Fact] public void AreaConvertsTsuboAsFraction() => Assert.Equal(400m / 121m, Convert(1, "area-tsubo", "area-m2"));
    [Fact] public void AreaConvertsTanAndCho() { Assert.Equal(300m, Convert(1, "area-tan", "area-tsubo")); Assert.Equal(3000m, Convert(1, "area-cho", "area-tsubo")); }
    [Fact] public void AreaConvertsAcreFromFeet() { Assert.Equal(43560m, Convert(1, "area-acre", "area-ft2")); Assert.Equal(4046.8564224m, Convert(1, "area-acre", "area-m2")); }
    [Fact] public void AreaConvertsSquareMilesToAcres() => Assert.Equal(640m, Convert(1, "area-mi2", "area-acre"));
    [Fact] public void AreaRoundTripPreservesValue() => Assert.Equal(1234.5m, Convert(Convert(1234.5m, "area-m2", "area-acre"), "area-acre", "area-m2"));

    [Fact] public void VolumeConvertsCubicMetersToLiters() => Assert.Equal(1000m, Convert(1, "volume-m3", "volume-l"));
    [Fact] public void VolumeConvertsCubicFeetExactly() => Assert.Equal(0.028316846592m, Convert(1, "volume-ft3", "volume-m3"));
    [Fact] public void VolumeConvertsCubicYardsToFeet() => Assert.Equal(27m, Convert(1, "volume-yd3", "volume-ft3"));
    [Fact] public void VolumeDerivesCubicInchFromInternationalInch() => Assert.Equal(0.0254m * 0.0254m * 0.0254m, Convert(1, "volume-in3", "volume-m3"));
    [Fact] public void VolumeRoundTripPreservesValue() => Assert.Equal(8.25m, Convert(Convert(8.25m, "volume-m3", "volume-ft3"), "volume-ft3", "volume-m3"));
    [Fact] public void DoesNotContainLiquidCapacityUnits() => Assert.DoesNotContain(UnitConversionService.Units, unit => unit.Id.Contains("gallon", StringComparison.OrdinalIgnoreCase));

    [Fact] public void ScaleDrawingToActualConvertsMillimetersToMeters() => Assert.Equal(25m, Assert.IsType<decimal>(_scale.DrawingToActual(1000, 25, "mm", "m").Value));
    [Fact] public void ScaleActualToDrawingConvertsMetersToMillimeters() => Assert.Equal(60m, Assert.IsType<decimal>(_scale.ActualToDrawing(500, 30, "m", "mm").Value));
    [Fact] public void ScaleSupportsOtherUnitsAndRoundTrips() { var actual = Assert.IsType<decimal>(_scale.DrawingToActual(2000, 100, "mm", "m").Value); Assert.Equal(200m, actual); Assert.Equal(100m, Assert.IsType<decimal>(_scale.ActualToDrawing(2000, actual, "m", "mm").Value)); Assert.Equal(5000m, Assert.IsType<decimal>(_scale.ActualToDrawing(1000, 5, "km", "mm").Value)); }
    [Fact] public void ScaleAllowsZeroLengthAndDecimals() { Assert.Equal(0m, Assert.IsType<decimal>(_scale.DrawingToActual(1000, 0, "mm", "m").Value)); Assert.Equal(1.25m, Assert.IsType<decimal>(_scale.DrawingToActual(1000, 1.25m, "mm", "m").Value)); }
    [Theory]
    [InlineData(0)] [InlineData(-1)]
    public void ScaleRejectsNonPositiveDenominator(decimal denominator) => Assert.False(_scale.DrawingToActual(denominator, 1, "mm", "m").IsSuccess);
    [Fact] public void ScaleRejectsNegativeLength() => Assert.False(_scale.ActualToDrawing(1000, -1, "m", "mm").IsSuccess);

    [Fact] public void UnitConversionRejectsNegativeInputAndMissingUnits() { Assert.False(_units.Convert(-1, "length-m", "length-mm").IsSuccess); Assert.False(_units.Convert(1, null, "length-mm").IsSuccess); }
    [Fact] public void UnitConversionHandlesDecimalOverflow() => Assert.Equal("数值超出可处理范围。", _units.Convert(decimal.MaxValue, "length-mi", "length-mm").Error);
    [Fact] public void UnitConversionAvoidsIntermediateOverflowWhenFinalValueFits() => Assert.Equal(decimal.MaxValue / 2_589_988_110_336m, Convert(decimal.MaxValue, "area-mm2", "area-mi2"));
    [Fact] public void TinyNonZeroValueRemainsNonZeroInternally() => Assert.NotEqual(0m, Convert(0.00000000000000000001m, "length-mm", "length-m"));

    [Fact]
    public void UnitCatalogIsValidAndAllSameCategoryUnitsRoundTrip()
    {
        Assert.Equal(UnitConversionService.Units.Count, UnitConversionService.Units.Select(unit => unit.Id).Distinct().Count());
        Assert.All(UnitConversionService.Units, unit => { Assert.True(unit.Numerator > 0); Assert.True(unit.Denominator > 0); });
        foreach (var category in Enum.GetValues<MeasurementCategory>())
        {
            var units = _units.GetUnits(category);
            Assert.Equal(units.Count, units.Select(unit => unit.SortOrder).Distinct().Count());
            foreach (var source in units)
            {
                Assert.Equal(7.25m, Convert(7.25m, source.Id, source.Id));
                foreach (var target in units)
                    AssertClose(7.25m, Convert(Convert(7.25m, source.Id, target.Id), target.Id, source.Id));
            }
        }
    }

    [Fact]
    public void UnitCatalogRejectsCrossCategoryConversionsAndForbiddenUnits()
    {
        Assert.False(_units.Convert(1, "length-m", "area-m2").IsSuccess);
        Assert.DoesNotContain(UnitConversionService.Units, unit => new[] { "gallon", "pint", "quart", "fluid ounce", "cup", "survey" }.Any(forbidden => unit.Id.Contains(forbidden, StringComparison.OrdinalIgnoreCase) || unit.Symbol.Contains(forbidden, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void ConversionFormattingUsesPrecisionWithoutChangingRawResult()
    {
        var result = ConversionResult.Success(1.23456m);
        Assert.Equal("1.23 m", ConversionResultFormatter.Format(result, 2, "m"));
        Assert.Equal(1.23456m, result.Value);
    }

    [Fact]
    public void ConversionFormattingDoesNotMisrepresentTinyNonZeroValueAsZero()
        => Assert.False(ConversionResultFormatter.Format(ConversionResult.Success(0.0000001m), 2, "m").StartsWith("0.00", StringComparison.Ordinal));
}
