using UrbanPlanToolbox.Services;
using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class ShapefileDatasetInspectorTests
{
    [Fact]
    public void MissingRequiredCompanionsAreReportedWithoutWritingFiles()
    {
        var folder = Path.Combine(Path.GetTempPath(), "UrbanPlanToolbox-shp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var result = new ShapefileCoordinateConversionService().Inspect(Path.Combine(folder, "source.shp"));
            Assert.False(result.HasShp); Assert.False(result.HasDbf); Assert.NotNull(result.Warning);
        }
        finally { Directory.Delete(folder, true); }
    }
}
