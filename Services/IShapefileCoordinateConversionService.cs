using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.Services;

public interface IShapefileCoordinateConversionService
{
    ShapefileDataset Inspect(string shpPath);
    ShapefileCompatibilityProfile GetCompatibility(string shpPath);
    Task<ShapefileConversionResult> ConvertAsync(ShapefileConversionRequest request, IProgress<ShapefileConversionProgress>? progress = null, CancellationToken cancellationToken = default);
}
