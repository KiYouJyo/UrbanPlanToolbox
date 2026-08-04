using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.Services;

/// <summary>Offline public-approximation conversion for general mapping and research support.</summary>
public sealed class CoordinateConversionService : ICoordinateConversionService
{
    private const double EarthRadius = 6378245.0, EccentricitySquared = 0.00669342162296594323, Pi = Math.PI, XPi = Pi * 3000.0 / 180.0;
    private const int MaxInverseIterations = 12;
    private const double InverseTolerance = 1e-9;

    public CoordinateConversionResult Convert(CoordinatePoint point, CoordinateSystemType source, CoordinateSystemType target)
    {
        if (!point.IsFinite || point.Longitude is < -180 or > 180 || point.Latitude is < -90 or > 90)
            return new(false, point, Error: "Longitude must be between -180 and 180 and latitude must be between -90 and 90.");
        if (source == target) return new(true, point, CoordinateConversionWarning.SameCoordinateSystem);
        return (source, target) switch
        {
            (CoordinateSystemType.Wgs84, CoordinateSystemType.Gcj02) => WgsToGcj(point),
            (CoordinateSystemType.Gcj02, CoordinateSystemType.Wgs84) => GcjToWgs(point),
            (CoordinateSystemType.Gcj02, CoordinateSystemType.Bd09) => new(true, GcjToBd(point)),
            (CoordinateSystemType.Bd09, CoordinateSystemType.Gcj02) => new(true, BdToGcj(point)),
            (CoordinateSystemType.Wgs84, CoordinateSystemType.Bd09) => WgsToBd(point),
            (CoordinateSystemType.Bd09, CoordinateSystemType.Wgs84) => GcjToWgs(BdToGcj(point)),
            _ => new(false, point, Error: "The requested coordinate conversion is not supported.")
        };
    }

    private static CoordinateConversionResult WgsToBd(CoordinatePoint point)
    {
        var gcj = WgsToGcj(point);
        return gcj with { Point = GcjToBd(gcj.Point) };
    }

    private static CoordinateConversionResult WgsToGcj(CoordinatePoint point) => IsOutsideChinaApproximationArea(point)
        ? new(true, point, CoordinateConversionWarning.OutsideChinaApproximationArea)
        : new(true, ApplyWgsOffset(point));

    private static CoordinateConversionResult GcjToWgs(CoordinatePoint point)
    {
        if (IsOutsideChinaApproximationArea(point)) return new(true, point, CoordinateConversionWarning.OutsideChinaApproximationArea);
        var estimate = point;
        for (var iteration = 1; iteration <= MaxInverseIterations; iteration++)
        {
            var forward = ApplyWgsOffset(estimate);
            var longitudeError = forward.Longitude - point.Longitude;
            var latitudeError = forward.Latitude - point.Latitude;
            estimate = new(estimate.Longitude - longitudeError, estimate.Latitude - latitudeError);
            if (Math.Max(Math.Abs(longitudeError), Math.Abs(latitudeError)) <= InverseTolerance)
                return new(true, estimate, Iterations: iteration);
        }
        return new(false, estimate, IsConverged: false, Iterations: MaxInverseIterations, Error: "GCJ-02 inverse conversion did not converge.");
    }

    private static CoordinatePoint ApplyWgsOffset(CoordinatePoint point)
    {
        var latitudeDelta = TransformLatitude(point.Longitude - 105, point.Latitude - 35);
        var longitudeDelta = TransformLongitude(point.Longitude - 105, point.Latitude - 35);
        var radians = point.Latitude / 180 * Pi;
        var magic = 1 - EccentricitySquared * Math.Sin(radians) * Math.Sin(radians);
        var sqrtMagic = Math.Sqrt(magic);
        latitudeDelta = latitudeDelta * 180 / ((EarthRadius * (1 - EccentricitySquared)) / (magic * sqrtMagic) * Pi);
        longitudeDelta = longitudeDelta * 180 / (EarthRadius / sqrtMagic * Math.Cos(radians) * Pi);
        return new(point.Longitude + longitudeDelta, point.Latitude + latitudeDelta);
    }

    private static CoordinatePoint GcjToBd(CoordinatePoint point)
    {
        var z = Math.Sqrt(point.Longitude * point.Longitude + point.Latitude * point.Latitude) + 0.00002 * Math.Sin(point.Latitude * XPi);
        var theta = Math.Atan2(point.Latitude, point.Longitude) + 0.000003 * Math.Cos(point.Longitude * XPi);
        return new(z * Math.Cos(theta) + 0.0065, z * Math.Sin(theta) + 0.006);
    }

    private static CoordinatePoint BdToGcj(CoordinatePoint point)
    {
        var x = point.Longitude - 0.0065; var y = point.Latitude - 0.006;
        var z = Math.Sqrt(x * x + y * y) - 0.00002 * Math.Sin(y * XPi);
        var theta = Math.Atan2(y, x) - 0.000003 * Math.Cos(x * XPi);
        return new(z * Math.Cos(theta), z * Math.Sin(theta));
    }

    private static bool IsOutsideChinaApproximationArea(CoordinatePoint point) => point.Longitude is < 72.004 or > 137.8347 || point.Latitude is < 0.8293 or > 55.8271;
    private static double TransformLatitude(double x, double y) => -100 + 2 * x + 3 * y + .2 * y * y + .1 * x * y + .2 * Math.Sqrt(Math.Abs(x)) + (20 * Math.Sin(6 * x * Pi) + 20 * Math.Sin(2 * x * Pi)) * 2 / 3 + (20 * Math.Sin(y * Pi) + 40 * Math.Sin(y / 3 * Pi)) * 2 / 3 + (160 * Math.Sin(y / 12 * Pi) + 320 * Math.Sin(y * Pi / 30)) * 2 / 3;
    private static double TransformLongitude(double x, double y) => 300 + x + 2 * y + .1 * x * x + .1 * x * y + .1 * Math.Sqrt(Math.Abs(x)) + (20 * Math.Sin(6 * x * Pi) + 20 * Math.Sin(2 * x * Pi)) * 2 / 3 + (20 * Math.Sin(x * Pi) + 40 * Math.Sin(x / 3 * Pi)) * 2 / 3 + (150 * Math.Sin(x / 12 * Pi) + 300 * Math.Sin(x / 30 * Pi)) * 2 / 3;
}
