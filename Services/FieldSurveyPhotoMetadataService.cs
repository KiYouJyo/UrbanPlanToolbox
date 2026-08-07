using System.Globalization;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Jpeg;
using UrbanPlanToolbox.Models;
using MetadataDirectory = MetadataExtractor.Directory;

namespace UrbanPlanToolbox.Services;

public sealed class FieldSurveyPhotoMetadataService
{
    public PhotoMetadataResult Read(string path)
    {
        using var stream = File.OpenRead(path);
        return Read(stream);
    }

    public PhotoMetadataResult Read(Stream stream)
    {
        try
        {
            var directories = ImageMetadataReader.ReadMetadata(stream);
            var gps = directories.OfType<GpsDirectory>().FirstOrDefault();
            var lat = ReadCoordinate(gps, GpsDirectory.TagLatitude, GpsDirectory.TagLatitudeRef);
            var lon = ReadCoordinate(gps, GpsDirectory.TagLongitude, GpsDirectory.TagLongitudeRef);
            var status = lat is null && lon is null ? PhotoGpsStatus.NoGps :
                lat is >= -90 and <= 90 && lon is >= -180 and <= 180 ? PhotoGpsStatus.Valid : PhotoGpsStatus.Invalid;
            var exif = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
            var ifd = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
            return new(
                ReadDate(exif, ExifDirectoryBase.TagDateTimeOriginal),
                status == PhotoGpsStatus.Valid ? lon : null,
                status == PhotoGpsStatus.Valid ? lat : null,
                ReadDouble(gps, GpsDirectory.TagAltitude),
                ReadDouble(gps, GpsDirectory.TagImgDirection),
                ifd?.GetDescription(ExifDirectoryBase.TagMake),
                ifd?.GetDescription(ExifDirectoryBase.TagModel),
                ReadInt(ifd, ExifDirectoryBase.TagOrientation), status);
        }
        catch { return new(null, null, null, null, null, null, null, null, PhotoGpsStatus.Invalid); }
    }

    private static DateTimeOffset? ReadDate(ExifSubIfdDirectory? directory, int tag)
    {
        var value = directory?.GetDescription(tag);
        return DateTime.TryParseExact(value, ["yyyy:MM:dd HH:mm:ss", "yyyy:MM:dd HH:mm:ss.FFF"], CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed) ? new DateTimeOffset(parsed) : null;
    }
    private static double? ReadDouble(MetadataDirectory? directory, int tag)
    {
        if (directory is null) return null;
        try { var value = directory.GetDouble(tag); return double.IsFinite(value) ? value : null; }
        catch { return double.TryParse(directory.GetDescription(tag), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && double.IsFinite(parsed) ? parsed : null; }
    }
    private static int? ReadInt(MetadataDirectory? directory, int tag) => int.TryParse(directory?.GetDescription(tag), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;
    private static double? ReadCoordinate(GpsDirectory? directory, int valueTag, int referenceTag)
    {
        if (directory is null) return null;
        try
        {
            var parts = directory.GetRationalArray(valueTag); if (parts is null || parts.Length < 3 || parts.Any(part => part.Denominator == 0)) return null;
            var value = (double)parts[0].Numerator / parts[0].Denominator + (double)parts[1].Numerator / parts[1].Denominator / 60d + (double)parts[2].Numerator / parts[2].Denominator / 3600d;
            var reference = directory.GetDescription(referenceTag)?.Trim().ToUpperInvariant(); if (reference is "S" or "W") value = -value;
            return double.IsFinite(value) ? value : null;
        }
        catch { return null; }
    }
}
