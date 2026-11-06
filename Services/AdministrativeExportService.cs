using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.Esri;
using NetTopologySuite.IO.Esri.Dbf.Fields;
using NetTopologySuite.IO.Esri.Shapefiles.Writers;

namespace UrbanPlanToolbox.Services;

/// <summary>Exports the exact selected polygons and their joined attributes; never dissolves or aggregates them.</summary>
public static class AdministrativeExportService
{
    public static void Export(string geojson, string format, string destination)
    {
        using var doc = JsonDocument.Parse(geojson);
        var rows = doc.RootElement.GetProperty("features").EnumerateArray().Select(f => f.Clone()).ToArray();
        if (rows.Length == 0) throw new InvalidDataException("No areas selected.");
        var fields = rows.SelectMany(f => f.GetProperty("properties").EnumerateObject().Select(p => p.Name)).Distinct().ToArray();
        var temporary = Path.Combine(Path.GetDirectoryName(destination)!, ".database-export-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try
        {
            var result = Path.Combine(temporary, "export." + (format == "shp" ? "zip" : format));
            if (format == "geojson") File.WriteAllText(result, geojson, new UTF8Encoding(false));
            else if (format == "csv") WriteCsv(result, rows, fields);
            else if (format == "shp")
            {
                var files = Path.Combine(temporary, "files"); Directory.CreateDirectory(files);
                var dbfFields = new List<DbfField>();
                var numericFields = fields.Select(name => rows.Any(row => row.GetProperty("properties").TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number) && rows.All(row => !row.GetProperty("properties").TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Number)).ToArray();
                for (var i = 0; i < fields.Length; i++)
                {
                    var name = "F" + i.ToString("D4", CultureInfo.InvariantCulture);
                    if (numericFields[i]) dbfFields.AddNumericDoubleField(name);
                    else dbfFields.AddCharacterField(name, 254);
                }
                var options = new ShapefileWriterOptions(ShapeType.Polygon, dbfFields.ToArray()) { Encoding = Encoding.UTF8 };
                using (var writer = Shapefile.OpenWrite(Path.Combine(files, "areas.shp"), options))
                {
                    foreach (var row in rows)
                    {
                        writer.Geometry = ReadGeometry(row.GetProperty("geometry"));
                        for (var i = 0; i < fields.Length; i++)
                        {
                            var exists = row.GetProperty("properties").TryGetProperty(fields[i], out var value);
                            dbfFields[i].Value = !exists || value.ValueKind == JsonValueKind.Null ? null : numericFields[i] ? value.GetDouble() : LimitUtf8(Value(value), 254);
                        }
                        writer.Write();
                    }
                }
                File.WriteAllText(Path.Combine(files, "areas.cpg"), "UTF-8", new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(files, "areas.prj"), "GEOGCS[\"WGS 84\",DATUM[\"WGS_1984\",SPHEROID[\"WGS 84\",6378137,298.257223563]],PRIMEM[\"Greenwich\",0],UNIT[\"degree\",0.0174532925199433]]");
                File.WriteAllText(Path.Combine(files, "fields.json"), JsonSerializer.Serialize(fields.Select((name, i) => new { field = "F" + i.ToString("D4"), name })));
                WriteCsv(Path.Combine(files, "attributes.csv"), rows, fields);
                File.WriteAllText(Path.Combine(files, "README.txt"), "Source: MLIT N03-2026, derivative geo.maderaojen.me. Coordinates: longitude/latitude WGS84. DBF names are mapped in fields.json; DBF strings are limited to 254 UTF-8 bytes. attributes.csv preserves full values. Geometry and boundary vintage: 2026-01-01. Statistical vintage and source are recorded in exported attributes; no temporal boundary conversion or aggregation is performed.");
                ZipFile.CreateFromDirectory(files, result);
            }
            else throw new InvalidDataException("Unsupported export format.");
            File.Move(result, destination, true);
        }
        finally { Directory.Delete(temporary, true); }
    }
    private static Geometry ReadGeometry(JsonElement geometry)
    {
        var factory = new GeometryFactory(new PrecisionModel(), 4326);
        var coordinates = geometry.GetProperty("coordinates");
        Polygon Polygon(JsonElement rings)
        {
            var all = rings.EnumerateArray().Select(r => factory.CreateLinearRing(r.EnumerateArray().Select(c => new Coordinate(c[0].GetDouble(), c[1].GetDouble())).ToArray())).ToArray();
            return factory.CreatePolygon(all[0], all.Skip(1).ToArray());
        }
        return geometry.GetProperty("type").GetString() switch
        {
            "Polygon" => factory.CreateMultiPolygon(new[] { Polygon(coordinates) }),
            "MultiPolygon" => factory.CreateMultiPolygon(coordinates.EnumerateArray().Select(Polygon).ToArray()),
            _ => throw new InvalidDataException("Only polygon boundaries are supported.")
        };
    }
    private static string LimitUtf8(string text, int limit)
    {
        var builder = new StringBuilder(); var bytes = 0;
        foreach (var rune in text.EnumerateRunes()) { bytes += rune.Utf8SequenceLength; if (bytes > limit) break; builder.Append(rune.ToString()); }
        return builder.ToString();
    }
    private static string Value(JsonElement value) => value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : value.ValueKind == JsonValueKind.Null ? "" : value.ToString();
    private static void WriteCsv(string path, JsonElement[] rows, string[] fields)
    {
        static string Escape(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
        using var writer = new StreamWriter(path, false, new UTF8Encoding(true));
        writer.WriteLine(string.Join(',', fields.Select(Escape)));
        foreach (var row in rows) writer.WriteLine(string.Join(',', fields.Select(f => Escape(row.GetProperty("properties").TryGetProperty(f, out var v) ? Value(v) : ""))));
    }
}
