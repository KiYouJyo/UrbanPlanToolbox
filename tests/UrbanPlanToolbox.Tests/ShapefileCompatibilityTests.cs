using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.Esri;
using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Services;
using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class ShapefileCompatibilityTests
{
    [Theory]
    [InlineData(SupportedShapeType.Point)]
    [InlineData(SupportedShapeType.MultiPoint)]
    [InlineData(SupportedShapeType.PolyLine)]
    [InlineData(SupportedShapeType.Polygon)]
    public async Task SupportedTwoDimensionalTypesRoundTrip(SupportedShapeType type)
    {
        var root = Path.Combine(Path.GetTempPath(), "UrbanPlanToolbox-shp-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var source = Path.Combine(root, "source.shp"); Shapefile.WriteAllFeatures([new Feature(CreateGeometry(type), new AttributesTable { { "name", "测试" }, { "count", 2 } })], source);
            var service = new ShapefileCoordinateConversionService(); var result = await service.ConvertAsync(new(source, root, "result", CoordinateSystemType.Wgs84, CoordinateSystemType.Gcj02));
            Assert.True(result.IsSuccess, result.Error); Assert.True(File.Exists(Path.Combine(root, "result.shx"))); Assert.True(File.Exists(Path.Combine(root, "result.dbf"))); Assert.True(File.Exists(Path.Combine(root, "result.cpg")));
            Assert.Single(Shapefile.ReadAllFeatures(result.OutputShpPath!));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void PointZIsExplicitlyRejected() => Assert.False(new ShapefileCoordinateConversionService().GetCompatibility(CreateHeader(11)).IsSupported);

    private static Geometry CreateGeometry(SupportedShapeType type) => type switch
    {
        SupportedShapeType.Point => new Point(116.4, 39.9),
        SupportedShapeType.MultiPoint => new MultiPoint([new Point(116.4, 39.9), new Point(116.5, 40)]),
        SupportedShapeType.PolyLine => new MultiLineString([new LineString([new Coordinate(116.4,39.9),new Coordinate(116.5,40)])]),
        _ => new Polygon(new LinearRing([new Coordinate(116.4,39.9),new Coordinate(116.5,39.9),new Coordinate(116.5,40),new Coordinate(116.4,39.9)]))
    };
    private static string CreateHeader(int type) { var path=Path.GetTempFileName(); using var stream=File.OpenWrite(path); stream.SetLength(100); stream.Position=32; stream.Write(BitConverter.GetBytes(type)); return path; }
}
