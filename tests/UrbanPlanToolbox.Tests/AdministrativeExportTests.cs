using System.IO.Compression;
using NetTopologySuite.IO.Esri;
using UrbanPlanToolbox.Services;
using Xunit;
namespace UrbanPlanToolbox.Tests;
public sealed class AdministrativeExportTests
{
    [Fact]
    public void ShapefileBatchPreservesPolygonsHolesAndJapaneseAttributes()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try
        {
            var json = """{"type":"FeatureCollection","features":[{"type":"Feature","properties":{"code":"01100","name":"札幌市","人口":0},"geometry":{"type":"Polygon","coordinates":[[[0,0],[4,0],[4,4],[0,4],[0,0]],[[1,1],[1,2],[2,2],[2,1],[1,1]]]}},{"type":"Feature","properties":{"code":"43101","name":"熊本市中央区","人口":null},"geometry":{"type":"MultiPolygon","coordinates":[[[[10,0],[11,0],[11,1],[10,1],[10,0]]],[[[12,0],[13,0],[13,1],[12,1],[12,0]]]]}}]}""";
            var zip = Path.Combine(dir, "out.zip"); AdministrativeExportService.Export(json, "shp", zip);
            var output = Path.Combine(dir, "read"); ZipFile.ExtractToDirectory(zip, output);
            foreach (var ext in new[] { "shp", "shx", "dbf", "prj", "cpg" }) Assert.True(File.Exists(Path.Combine(output, "areas." + ext)));
            var features = Shapefile.ReadAllFeatures(Path.Combine(output, "areas.shp")).ToArray();
            Assert.Equal(2, features.Length); Assert.Equal(15, features[0].Geometry.Area); Assert.Equal(2, features[1].Geometry.Area);
            Assert.Equal(0d, features[0].Attributes["F0002"]); Assert.Null(features[1].Attributes["F0002"]);
            Assert.Equal("01100", features[0].Attributes["F0000"]); Assert.Equal("札幌市", features[0].Attributes["F0001"]);
            Assert.Contains("熊本市中央区", File.ReadAllText(Path.Combine(output, "attributes.csv")));
        }
        finally { Directory.Delete(dir, true); }
    }
}
