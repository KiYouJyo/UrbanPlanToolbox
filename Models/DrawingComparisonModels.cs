namespace UrbanPlanToolbox.Models;

public enum DrawingViewMode { Overlay, Wipe }

public sealed record DrawingInput(string Path, int PdfPage = 1);

public sealed record DifferenceRegion(int Id, int X, int Y, int Width, int Height, int Area);

public sealed record DifferenceResult(
    bool Succeeded,
    byte[] MaskPng,
    byte[] ResultPng,
    IReadOnlyList<DifferenceRegion> Regions,
    int AddedPixels,
    int DeletedPixels,
    string? ErrorMessage = null);

public sealed record ComparisonExportResult(bool Succeeded, string? Path, string? ErrorMessage = null);
public sealed record DrawingSizeValidationResult(bool IsValid, int WidthA, int HeightA, int WidthB, int HeightB);
