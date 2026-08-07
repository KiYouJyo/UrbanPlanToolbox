using System.Globalization;
using System.Text;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.Esri;
using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.Services;

public sealed class FieldSurveyExportService(FieldSurveyNamingService? naming = null)
{
    private readonly FieldSurveyNamingService _naming = naming ?? new();
    public Task<FieldSurveyExportResult> ExportAsync(IReadOnlyList<FieldSurveyPhoto> photos, FieldSurveyExportOptions options, CancellationToken cancellationToken = default) => Task.Run(() => Export(photos, options, cancellationToken), cancellationToken);
    private FieldSurveyExportResult Export(IReadOnlyList<FieldSurveyPhoto> photos, FieldSurveyExportOptions options, CancellationToken token)
    {
        if (photos.Count == 0) return new(false, Error: "No photos selected."); if (!Directory.Exists(options.OutputDirectory)) return new(false, Error: "Output directory is invalid.");
        var stage = Path.Combine(options.OutputDirectory, ".field-survey-" + Guid.NewGuid().ToString("N")); var final = Path.Combine(options.OutputDirectory, $"FieldSurvey_{DateTime.Now:yyyyMMdd_HHmmss}");
        try { Directory.CreateDirectory(Path.Combine(stage, "Photos")); Directory.CreateDirectory(Path.Combine(stage, "GIS")); var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var rows = new List<(FieldSurveyPhoto Photo, string Name)>();
            foreach (var photo in photos) { token.ThrowIfCancellationRequested(); var name = _naming.BuildFileName(photo, options.NameTemplate); var baseName = Path.GetFileNameWithoutExtension(name); var ext = Path.GetExtension(name); var n = 2; while (!names.Add(name)) name = $"{baseName}_{n++}{ext}"; File.Copy(photo.SourcePath, Path.Combine(stage, "Photos", name)); rows.Add((photo, name)); }
            WriteCsv(Path.Combine(stage, "SurveyPhotos.csv"), rows); var gps = rows.Where(x => x.Photo.GpsStatus == PhotoGpsStatus.Valid && x.Photo.Longitude is not null && x.Photo.Latitude is not null).ToArray(); if (gps.Length > 0) WriteShapefile(Path.Combine(stage, "GIS", "SurveyPhotos.shp"), gps); Directory.Move(stage, final); return new(true, final, rows.Count, gps.Length);
        } catch (Exception ex) { if (Directory.Exists(stage)) Directory.Delete(stage, true); return new(false, Error: ex is OperationCanceledException ? "Export cancelled." : "Export failed."); }
    }
    private static void WriteShapefile(string path, IEnumerable<(FieldSurveyPhoto Photo, string Name)> rows) { var features = rows.Select(x => new Feature(new Point(x.Photo.Longitude!.Value, x.Photo.Latitude!.Value), new AttributesTable { { "ID", x.Photo.Id }, { "PHOTO", "../Photos/" + x.Name }, { "ORIGNAME", x.Photo.OriginalName }, { "DATE", x.Photo.CapturedAt?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "" }, { "TIME", x.Photo.CapturedAt?.ToString("HH:mm:ss", CultureInfo.InvariantCulture) ?? "" }, { "LON", x.Photo.Longitude.Value }, { "LAT", x.Photo.Latitude.Value }, { "ALT", x.Photo.Altitude }, { "HEADING", x.Photo.Heading }, { "CAMERA", (x.Photo.Make + " " + x.Photo.Model).Trim() }, { "TAGS", Limit(string.Join(';', x.Photo.Tags), 240) }, { "NOTE", Limit(x.Photo.Note, 240) }, { "HASGPS", 1 } })).ToArray(); Shapefile.WriteAllFeatures(features, path); File.WriteAllText(Path.ChangeExtension(path, ".prj"), "GEOGCS[\"WGS 84\",DATUM[\"WGS_1984\",SPHEROID[\"WGS 84\",6378137,298.257223563]],PRIMEM[\"Greenwich\",0],UNIT[\"degree\",0.0174532925199433]]", Encoding.ASCII); File.WriteAllText(Path.ChangeExtension(path, ".cpg"), "UTF-8", new UTF8Encoding(false)); }
    private static string Limit(string value, int length) => value.Length <= length ? value : value[..length];
    private static void WriteCsv(string path, IEnumerable<(FieldSurveyPhoto Photo, string Name)> rows) { using var writer = new StreamWriter(path, false, new UTF8Encoding(true)); writer.WriteLine("ID,PHOTO,ORIGNAME,DATE,TIME,LON,LAT,ALT,HEADING,CAMERA,TAGS,NOTE,HASGPS"); foreach (var x in rows) { var p = x.Photo; writer.WriteLine(string.Join(',', new[] { p.Id, "Photos/" + x.Name, p.OriginalName, p.CapturedAt?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "", p.CapturedAt?.ToString("HH:mm:ss", CultureInfo.InvariantCulture) ?? "", p.Longitude?.ToString("R", CultureInfo.InvariantCulture) ?? "", p.Latitude?.ToString("R", CultureInfo.InvariantCulture) ?? "", p.Altitude?.ToString("R", CultureInfo.InvariantCulture) ?? "", p.Heading?.ToString("R", CultureInfo.InvariantCulture) ?? "", (p.Make + " " + p.Model).Trim(), string.Join(';', p.Tags), p.Note, p.GpsStatus == PhotoGpsStatus.Valid ? "1" : "0" }.Select(Escape))); } }
    private static string Escape(string value) => "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
}
