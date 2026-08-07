using System.Collections.ObjectModel;

namespace UrbanPlanToolbox.Models;

public enum PhotoGpsStatus { NoGps, Valid, Invalid }

public sealed class FieldSurveyPhoto
{
    public string SourcePath { get; set; } = string.Empty;
    public string OriginalName => Path.GetFileName(SourcePath);
    public string Id { get; set; } = string.Empty;
    public DateTimeOffset? CapturedAt { get; set; }
    public double? Longitude { get; set; }
    public double? Latitude { get; set; }
    public double? Altitude { get; set; }
    public double? Heading { get; set; }
    public string? Make { get; set; }
    public string? Model { get; set; }
    public int? Orientation { get; set; }
    public PhotoGpsStatus GpsStatus { get; set; }
    public ObservableCollection<string> Tags { get; } = [];
    public string Note { get; set; } = string.Empty;
    public bool IsAnnotated => Tags.Count > 0 || !string.IsNullOrWhiteSpace(Note);
}

public sealed record PhotoImportResult(IReadOnlyList<FieldSurveyPhoto> Photos, IReadOnlyList<string> UnsupportedFiles, IReadOnlyList<string> FailedFiles, int DuplicateCount);
public sealed record PhotoMetadataResult(DateTimeOffset? CapturedAt, double? Longitude, double? Latitude, double? Altitude, double? Heading, string? Make, string? Model, int? Orientation, PhotoGpsStatus GpsStatus);
public sealed record FieldSurveyExportOptions(string OutputDirectory, string NameTemplate = "{ID}_{Date}_{Time}");
public sealed record FieldSurveyExportResult(bool IsSuccess, string? OutputDirectory = null, int PhotoCount = 0, int GpsCount = 0, string? Error = null);
