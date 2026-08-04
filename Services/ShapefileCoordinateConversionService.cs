using System.Text.Json;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.Esri;
using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.Services;

/// <summary>Local, atomic Shapefile conversion. Source files are never modified or retained.</summary>
public sealed class ShapefileCoordinateConversionService(ICoordinateConversionService? coordinateConversion = null) : IShapefileCoordinateConversionService
{
    private readonly ICoordinateConversionService _coordinateConversion = coordinateConversion ?? new CoordinateConversionService();

    public ShapefileDataset Inspect(string shpPath)
    {
        var fullPath = Path.GetFullPath(shpPath);
        var basePath = Path.Combine(Path.GetDirectoryName(fullPath) ?? string.Empty, Path.GetFileNameWithoutExtension(fullPath));
        var hasShp = File.Exists(basePath + ".shp"); var hasDbf = File.Exists(basePath + ".dbf"); var hasShx = File.Exists(basePath + ".shx");
        var warning = !hasShp || !hasDbf ? "The .shp and .dbf files are required." : !hasShx ? "The .shx index is missing; the reader may still open this dataset, but output will regenerate an index." : null;
        return new(fullPath, hasShp, hasDbf, hasShx, File.Exists(basePath + ".prj"), File.Exists(basePath + ".cpg"), warning);
    }

    public ShapefileCompatibilityProfile GetCompatibility(string shpPath)
    {
        if (!File.Exists(shpPath) || new FileInfo(shpPath).Length < 36) return new(false, "Unknown", "The Shapefile header is invalid.");
        using var stream = File.OpenRead(shpPath); stream.Position = 32; Span<byte> bytes = stackalloc byte[4]; stream.ReadExactly(bytes);
        var type = BitConverter.ToInt32(bytes);
        return Enum.IsDefined(typeof(SupportedShapeType), type)
            ? new(true, ((SupportedShapeType)type).ToString())
            : new(false, type.ToString(System.Globalization.CultureInfo.InvariantCulture), "当前版本无法在不丢失几何信息的情况下转换此Shapefile类型。Z、M 和 NullShape 数据集不会被降级处理。");
    }

    public Task<ShapefileConversionResult> ConvertAsync(ShapefileConversionRequest request, IProgress<ShapefileConversionProgress>? progress = null, CancellationToken cancellationToken = default) => Task.Run(() => Convert(request, progress, cancellationToken), cancellationToken);

    private ShapefileConversionResult Convert(ShapefileConversionRequest request, IProgress<ShapefileConversionProgress>? progress, CancellationToken cancellationToken)
    {
        var dataset = Inspect(request.SourceShpPath);
        if (!dataset.HasShp || !dataset.HasDbf) return new(false, Error: dataset.Warning);
        var compatibility = GetCompatibility(dataset.ShpPath);
        if (!compatibility.IsSupported) return new(false, Error: compatibility.Issue);
        if (request.Source == request.Target) return new(false, Error: "Source and target coordinate systems must differ for Shapefile conversion.");
        if (!Directory.Exists(request.OutputDirectory)) return new(false, Error: "The selected output directory does not exist.");
        if (string.IsNullOrWhiteSpace(request.OutputName) || request.OutputName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return new(false, Error: "Output name is invalid.");
        var finalBase = Path.Combine(Path.GetFullPath(request.OutputDirectory), request.OutputName);
        if (Directory.EnumerateFiles(request.OutputDirectory, request.OutputName + ".*", SearchOption.TopDirectoryOnly).Any()) return new(false, Error: "Output files already exist.");
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), "UrbanPlanToolbox", "shapefile-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);
        try
        {
            var temporaryShp = Path.Combine(temporaryDirectory, request.OutputName + ".shp");
            var features = new List<IFeature>(); int vertices = 0, warnings = 0;
            foreach (var feature in Shapefile.ReadAllFeatures(dataset.ShpPath))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var copy = new Feature(feature.Geometry?.Copy(), feature.Attributes);
                if (copy.Geometry is not null) foreach (var point in copy.Geometry.Coordinates)
                {
                    var result = _coordinateConversion.Convert(new(point.X, point.Y), request.Source, request.Target);
                    if (!result.IsSuccess) throw new InvalidDataException(result.Error);
                    if (result.Warning == CoordinateConversionWarning.OutsideChinaApproximationArea) warnings++;
                    point.X = result.Point.Longitude; point.Y = result.Point.Latitude; vertices++;
                }
                features.Add(copy); progress?.Report(new(features.Count, vertices, warnings));
            }
            Shapefile.WriteAllFeatures(features, temporaryShp);
            File.WriteAllText(Path.Combine(temporaryDirectory, request.OutputName + ".cpg"), "UTF-8");
            WriteCoordinateMetadata(Path.Combine(temporaryDirectory, request.OutputName + ".coordinate-system.json"), request.Source, request.Target);
            if (request.Target == CoordinateSystemType.Wgs84) File.WriteAllText(Path.Combine(temporaryDirectory, request.OutputName + ".prj"), "GEOGCS[\"WGS 84\",DATUM[\"WGS_1984\",SPHEROID[\"WGS 84\",6378137,298.257223563]],PRIMEM[\"Greenwich\",0],UNIT[\"degree\",0.0174532925199433]]");
            foreach (var file in Directory.GetFiles(temporaryDirectory)) File.Move(file, Path.Combine(request.OutputDirectory, Path.GetFileName(file)));
            return new(true, finalBase + ".shp", features.Count, vertices, warnings);
        }
        catch (OperationCanceledException) { return new(false, Error: "Conversion was cancelled."); }
        catch (Exception ex) { return new(false, Error: ex.Message); }
        finally { if (Directory.Exists(temporaryDirectory)) Directory.Delete(temporaryDirectory, true); }
    }

    private static void WriteCoordinateMetadata(string path, CoordinateSystemType source, CoordinateSystemType target) => File.WriteAllText(path, JsonSerializer.Serialize(new { formatVersion = 1, sourceCoordinateSystem = source.ToString().ToUpperInvariant(), targetCoordinateSystem = target.ToString().ToUpperInvariant(), algorithm = "public-approximation", isStandardEpsg = false, app = "UrbanPlanToolbox", appVersion = AppVersionProvider.Version }));
}
