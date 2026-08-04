namespace UrbanPlanToolbox.Models;

public enum CoordinateSystemType { Wgs84, Gcj02, Bd09 }

public readonly record struct CoordinatePoint(double Longitude, double Latitude)
{
    public bool IsFinite => double.IsFinite(Longitude) && double.IsFinite(Latitude);
}

public enum CoordinateConversionWarning { None, OutsideChinaApproximationArea, SameCoordinateSystem }

public sealed record CoordinateConversionResult(
    bool IsSuccess, CoordinatePoint Point, CoordinateConversionWarning Warning = CoordinateConversionWarning.None,
    bool IsConverged = true, int Iterations = 0, string? Error = null);
