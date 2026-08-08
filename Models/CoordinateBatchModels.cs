namespace UrbanPlanToolbox.Models;

public enum CoordinateTextFormat { DecimalDegrees, DegreesDecimalMinutes, DegreesMinutesSeconds, Unknown }
public enum CoordinateOrder { Auto, LongitudeLatitude, LatitudeLongitude }
public enum CoordinateRowStatus { Success, Warning, Error }

public sealed record NormalizedCoordinate(double Longitude, double Latitude);

public sealed record CoordinateParseResult(
    bool IsSuccess,
    NormalizedCoordinate? Coordinate,
    CoordinateTextFormat DetectedFormat,
    CoordinateRowStatus Status,
    string Message);

public sealed record CoordinateBatchRow(
    string Id,
    string OriginalText,
    IReadOnlyDictionary<string, string> Fields,
    CoordinateParseResult Result);

public sealed record CoordinateBatchResult(IReadOnlyList<CoordinateBatchRow> Rows)
{
    public int Total => Rows.Count;
    public int SuccessCount => Rows.Count(row => row.Result.Status == CoordinateRowStatus.Success);
    public int WarningCount => Rows.Count(row => row.Result.Status == CoordinateRowStatus.Warning);
    public int ErrorCount => Rows.Count(row => row.Result.Status == CoordinateRowStatus.Error);
}
