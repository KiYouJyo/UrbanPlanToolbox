using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Services;
using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class CoordinateConversionServiceTests
{
    private readonly ICoordinateConversionService _service = new CoordinateConversionService();

    [Theory]
    [InlineData(116.397128, 39.916527)] // Beijing
    [InlineData(121.473701, 31.230416)] // Shanghai
    [InlineData(113.264385, 23.129112)] // Guangzhou
    [InlineData(120.155070, 30.274085)] // Hangzhou
    [InlineData(104.066541, 30.572269)] // Chengdu
    public void SupportsAllSixDirectionsWithRoundTripTolerance(double longitude, double latitude)
    {
        var wgs = new CoordinatePoint(longitude, latitude);
        var gcj = MustConvert(wgs, CoordinateSystemType.Wgs84, CoordinateSystemType.Gcj02);
        var bd = MustConvert(wgs, CoordinateSystemType.Wgs84, CoordinateSystemType.Bd09);
        AssertClose(wgs, MustConvert(gcj, CoordinateSystemType.Gcj02, CoordinateSystemType.Wgs84), 1e-6);
        AssertClose(gcj, MustConvert(bd, CoordinateSystemType.Bd09, CoordinateSystemType.Gcj02), 2e-6);
        AssertClose(wgs, MustConvert(bd, CoordinateSystemType.Bd09, CoordinateSystemType.Wgs84), 3e-6);
        Assert.True(_service.Convert(gcj, CoordinateSystemType.Gcj02, CoordinateSystemType.Bd09).IsSuccess);
        Assert.True(_service.Convert(bd, CoordinateSystemType.Bd09, CoordinateSystemType.Gcj02).IsSuccess);
    }

    [Fact]
    public void OutsideApproximationAreaRetainsPointAndReportsWarning()
    {
        var point = new CoordinatePoint(2.3522, 48.8566);
        var result = _service.Convert(point, CoordinateSystemType.Wgs84, CoordinateSystemType.Gcj02);
        Assert.True(result.IsSuccess); Assert.Equal(point, result.Point); Assert.Equal(CoordinateConversionWarning.OutsideChinaApproximationArea, result.Warning);
    }

    [Theory]
    [InlineData(double.NaN, 0)]
    [InlineData(181, 0)]
    [InlineData(0, 91)]
    public void RejectsInvalidCoordinates(double longitude, double latitude) => Assert.False(_service.Convert(new(longitude, latitude), CoordinateSystemType.Wgs84, CoordinateSystemType.Gcj02).IsSuccess);

    [Fact]
    public void SameSystemDoesNotPerformPseudoConversion()
    {
        var point = new CoordinatePoint(116.4, 39.9); var result = _service.Convert(point, CoordinateSystemType.Wgs84, CoordinateSystemType.Wgs84);
        Assert.True(result.IsSuccess); Assert.Equal(point, result.Point); Assert.Equal(CoordinateConversionWarning.SameCoordinateSystem, result.Warning);
    }

    private CoordinatePoint MustConvert(CoordinatePoint point, CoordinateSystemType source, CoordinateSystemType target)
    {
        var result = _service.Convert(point, source, target); Assert.True(result.IsSuccess, result.Error); return result.Point;
    }
    private static void AssertClose(CoordinatePoint expected, CoordinatePoint actual, double tolerance)
    { Assert.InRange(Math.Abs(expected.Longitude - actual.Longitude), 0, tolerance); Assert.InRange(Math.Abs(expected.Latitude - actual.Latitude), 0, tolerance); }
}
