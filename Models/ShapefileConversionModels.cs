namespace UrbanPlanToolbox.Models;

public sealed record ShapefileDataset(string ShpPath, bool HasShp, bool HasDbf, bool HasShx, bool HasPrj, bool HasCpg, string? Warning = null);
public enum SupportedShapeType { Point = 1, PolyLine = 3, Polygon = 5, MultiPoint = 8 }
public sealed record ShapefileCompatibilityProfile(bool IsSupported, string ShapeType, string? Issue = null);
public sealed record ShapefileConversionRequest(string SourceShpPath, string OutputDirectory, string OutputName, CoordinateSystemType Source, CoordinateSystemType Target);
public sealed record ShapefileConversionProgress(int FeaturesProcessed, int VerticesProcessed, int Warnings);
public sealed record ShapefileConversionResult(bool IsSuccess, string? OutputShpPath = null, int FeaturesProcessed = 0, int VerticesProcessed = 0, int Warnings = 0, string? Error = null);
