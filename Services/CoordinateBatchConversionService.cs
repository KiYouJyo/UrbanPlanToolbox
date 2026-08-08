using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.Services;

public sealed class CoordinateBatchConversionService
{
    private static readonly Regex DirectionRegex = new("(?<dir>E|W|N|S|东经|西经|北纬|南纬|東経|西経|北緯|南緯)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ComponentRegex = new(@"(?<prefix>E|W|N|S)?\s*(?<value>[+-]?\d+(?:\.\d+)?(?:\s*°\s*\d+(?:\.\d+)?(?:\s*′(?:\s*\d+(?:\.\d+)?\s*″?)?)?)?)(?:\s*(?<suffix>E|W|N|S))?", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public CoordinateParseResult ParsePair(string? text, CoordinateOrder order = CoordinateOrder.Auto) 
    {
        var normalized = Normalize(text);
        if (string.IsNullOrWhiteSpace(normalized)) return Error("Empty coordinate.");

        var components = ExtractComponents(normalized);
        if (components.Count != 2)
        {
            components = SplitPlainPair(normalized);
        }
        if (components.Count != 2) return Error("Unable to parse two coordinate values.");

        var first = ParseComponent(components[0].Value, components[0].Direction);
        var second = ParseComponent(components[1].Value, components[1].Direction);
        if (!first.IsSuccess) return new(false, null, first.DetectedFormat, first.Status, first.Message);
        if (!second.IsSuccess) return new(false, null, second.DetectedFormat, second.Status, second.Message);

        var format = first.DetectedFormat == second.DetectedFormat ? first.DetectedFormat : CoordinateTextFormat.Unknown;
        var selectedOrder = order;
        var warning = false;
        if (selectedOrder == CoordinateOrder.Auto)
        {
            if (Math.Abs(first.Value!.Value) > 90 && Math.Abs(second.Value!.Value) <= 90) selectedOrder = CoordinateOrder.LongitudeLatitude;
            else if (Math.Abs(first.Value!.Value) <= 90 && Math.Abs(second.Value!.Value) > 90) { selectedOrder = CoordinateOrder.LatitudeLongitude; warning = true; }
            else if (Math.Abs(first.Value!.Value) <= 90 && Math.Abs(second.Value!.Value) <= 90) return new(false, null, format, CoordinateRowStatus.Warning, "Ambiguous coordinate order; choose Longitude/Latitude or Latitude/Longitude.");
            else return Error("Both coordinate values exceed the latitude range.");
        }

        var longitude = selectedOrder == CoordinateOrder.LatitudeLongitude ? second.Value!.Value : first.Value!.Value;
        var latitude = selectedOrder == CoordinateOrder.LatitudeLongitude ? first.Value!.Value : second.Value!.Value;
        var range = Validate(longitude, latitude);
        if (range is not null) return new(false, null, format, CoordinateRowStatus.Error, range);
        var message = warning ? "Detected possible Latitude/Longitude order; values were exchanged." : "";
        return new(true, new(longitude, latitude), format, warning ? CoordinateRowStatus.Warning : CoordinateRowStatus.Success, message);
    }

    public CoordinateBatchResult ParseRows(IEnumerable<IReadOnlyDictionary<string, string>> rows, string? longitudeField, string? latitudeField, string? combinedField, CoordinateOrder order = CoordinateOrder.Auto)
    {
        var output = new List<CoordinateBatchRow>(); var index = 0;
        foreach (var row in rows)
        {
            index++;
            row.TryGetValue(combinedField ?? string.Empty, out var combined);
            var original = combined;
            if (string.IsNullOrWhiteSpace(combined) && longitudeField is not null && latitudeField is not null)
            {
                row.TryGetValue(longitudeField, out var lon); row.TryGetValue(latitudeField, out var lat); original = $"{lon} {lat}";
            }
            var result = ParsePair(original, order);
            output.Add(new(index.ToString(CultureInfo.InvariantCulture), original ?? string.Empty, row, result));
        }
        return new(output);
    }

    public static IReadOnlyList<IReadOnlyDictionary<string, string>> ParseDelimited(string text, char delimiter)
    {
        var records = new List<List<string>>(); var row = new List<string>(); var field = new StringBuilder(); var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '"') { if (quoted && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; } else quoted = !quoted; }
            else if (c == delimiter && !quoted) { row.Add(field.ToString()); field.Clear(); }
            else if ((c == '\r' || c == '\n') && !quoted) { if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++; row.Add(field.ToString()); field.Clear(); if (row.Any(value => value.Length > 0)) records.Add(row); row = new(); }
            else field.Append(c);
        }
        if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); records.Add(row); }
        if (records.Count == 0) return [];
        var headers = records[0]; return records.Skip(1).Select(values => (IReadOnlyDictionary<string, string>)headers.Select((header, index) => new { header, index }).ToDictionary(x => x.header, x => x.index < values.Count ? values[x.index] : string.Empty, StringComparer.OrdinalIgnoreCase)).ToArray();
    }

    public static char DetectDelimiter(string text) => new[] { '\t', ',', ';' }.OrderByDescending(delimiter => text.Split(delimiter).Length).First();

    public static string ExportCsv(IEnumerable<CoordinateBatchRow> rows, CoordinateTextFormat format = CoordinateTextFormat.DecimalDegrees, int decimals = 6)
    {
        var output = new StringBuilder("ID,OriginalText,Longitude,Latitude,DetectedFormat,Status,Message\r\n");
        foreach (var row in rows)
        {
            var coordinate = row.Result.Coordinate;
            var longitude = coordinate is null ? "" : Csv(FormatSingle(coordinate.Longitude, true, format, decimals));
            var latitude = coordinate is null ? "" : Csv(FormatSingle(coordinate.Latitude, false, format, decimals));
            output.Append(string.Join(',', Csv(row.Id), Csv(row.OriginalText), longitude, latitude, row.Result.DetectedFormat, row.Result.Status, Csv(row.Result.Message))).Append("\r\n");
        }
        return output.ToString();
    }
    private static string Csv(string value) => value.Contains(',', StringComparison.Ordinal) || value.Contains('"') || value.Contains('\n') ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"" : value;

    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var value = text.Trim()
            .Replace('º', '°').Replace('˚', '°').Replace('′', '′').Replace('’', '′').Replace('‘', '′').Replace('\'', '′')
            .Replace('″', '″').Replace('”', '″').Replace('“', '″').Replace('"', '″')
            .Normalize(NormalizationForm.FormKC)
            .Replace("o", "°", StringComparison.Ordinal).Replace("′′", "″", StringComparison.Ordinal);
        return value.Replace("东经", "E", StringComparison.Ordinal).Replace("西经", "W", StringComparison.Ordinal)
            .Replace("北纬", "N", StringComparison.Ordinal).Replace("南纬", "S", StringComparison.Ordinal)
            .Replace("東経", "E", StringComparison.Ordinal).Replace("西経", "W", StringComparison.Ordinal)
            .Replace("北緯", "N", StringComparison.Ordinal).Replace("南緯", "S", StringComparison.Ordinal);
    }

    public static string Format(NormalizedCoordinate coordinate, CoordinateTextFormat format, int decimals = 6)
    {
        decimals = Math.Clamp(decimals, 0, 12);
        return format switch
        {
            CoordinateTextFormat.DecimalDegrees => $"{FormatSingle(coordinate.Longitude, true, format, decimals)},{FormatSingle(coordinate.Latitude, false, format, decimals)}",
            CoordinateTextFormat.DegreesDecimalMinutes => $"{FormatSingle(coordinate.Longitude, true, format, decimals)}\t{FormatSingle(coordinate.Latitude, false, format, decimals)}",
            CoordinateTextFormat.DegreesMinutesSeconds => $"{FormatSingle(coordinate.Longitude, true, format, decimals)}\t{FormatSingle(coordinate.Latitude, false, format, decimals)}",
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
    }

    public static string FormatSingle(double value, bool longitude, CoordinateTextFormat format, int decimals = 6)
    {
        decimals = Math.Clamp(decimals, 0, 12);
        return format switch
        {
            CoordinateTextFormat.DecimalDegrees => value.ToString($"F{decimals}", CultureInfo.InvariantCulture),
            CoordinateTextFormat.DegreesDecimalMinutes => FormatDdm(value, longitude, decimals),
            CoordinateTextFormat.DegreesMinutesSeconds => FormatDms(value, longitude, decimals),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
    }

    private static List<(string Value, string? Direction)> ExtractComponents(string text)
    {
        var matches = ComponentRegex.Matches(text); var list = new List<(string, string?)>();
        foreach (Match match in matches)
        {
            if (!match.Groups["value"].Success) continue;
            var dir = match.Groups["prefix"].Value + match.Groups["suffix"].Value;
            list.Add((match.Groups["value"].Value, string.IsNullOrEmpty(dir) ? null : dir));
        }
        return list;
    }

    private static List<(string Value, string? Direction)> SplitPlainPair(string text)
    {
        var parts = text.Split([',', ';', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2) parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 2 ? parts.Select(part => (part, (string?)null)).ToList() : [];
    }

    private static (bool IsSuccess, double? Value, CoordinateTextFormat DetectedFormat, CoordinateRowStatus Status, string Message) ParseComponent(string text, string? direction)
    {
        var format = text.Contains('″') ? CoordinateTextFormat.DegreesMinutesSeconds : text.Contains('′') ? CoordinateTextFormat.DegreesDecimalMinutes : CoordinateTextFormat.DecimalDegrees;
        double value;
        string[]? dms = null; string[]? ddm = null;
        if (format == CoordinateTextFormat.DegreesMinutesSeconds && !TryMatch(text, @"^([+-]?\d+(?:\.\d+)?)°\s*(\d+(?:\.\d+)?)′\s*(\d+(?:\.\d+)?)″$", out dms)) return Failure(format, "Invalid degrees/minutes/seconds.");
        else if (format == CoordinateTextFormat.DegreesMinutesSeconds)
        {
            var minutes = double.Parse(dms![1], CultureInfo.InvariantCulture); var seconds = double.Parse(dms[2], CultureInfo.InvariantCulture);
            if (minutes is < 0 or >= 60) return Failure(format, "Minutes must be less than 60.");
            if (seconds is < 0 or >= 60) return Failure(format, "Seconds must be less than 60.");
            value = double.Parse(dms[0], CultureInfo.InvariantCulture) + minutes / 60 + seconds / 3600;
        }
        else if (format == CoordinateTextFormat.DegreesDecimalMinutes && !TryMatch(text, @"^([+-]?\d+(?:\.\d+)?)°\s*(\d+(?:\.\d+)?)′$", out ddm)) return Failure(format, "Invalid degrees/decimal-minutes.");
        else if (format == CoordinateTextFormat.DegreesDecimalMinutes)
        {
            var minutes = double.Parse(ddm![1], CultureInfo.InvariantCulture);
            if (minutes is < 0 or >= 60) return Failure(format, "Minutes must be less than 60.");
            value = double.Parse(ddm[0], CultureInfo.InvariantCulture) + minutes / 60;
        }
        else if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return Failure(format, "Invalid decimal degree.");
        var negative = text.TrimStart().StartsWith('-'); var dir = direction?.ToUpperInvariant();
        if (negative && dir is "E" or "N") return Failure(format, "Sign conflicts with direction marker.");
        if (dir is "W" or "S") value = -Math.Abs(value); else if (dir is "E" or "N") value = Math.Abs(value); else if (negative) value = -Math.Abs(value);
        if (Math.Abs(value) > 180) return Failure(format, "Coordinate value is out of range.");
        return (true, value, format, CoordinateRowStatus.Success, "");
    }

    private static bool TryMatch(string text, string pattern, out string[]? groups) { var m = Regex.Match(text, pattern); groups = m.Success ? m.Groups.Cast<Group>().Skip(1).Select(g => g.Value).ToArray() : null; return m.Success; }
    private static (bool, double?, CoordinateTextFormat, CoordinateRowStatus, string) Failure(CoordinateTextFormat format, string message) => (false, null, format, CoordinateRowStatus.Error, message);
    private static CoordinateParseResult Error(string message) => new(false, null, CoordinateTextFormat.Unknown, CoordinateRowStatus.Error, message);
    private static string? Validate(double longitude, double latitude) => longitude is < -180 or > 180 ? "Longitude is out of range." : latitude is < -90 or > 90 ? "Latitude is out of range." : null;
    private static string FormatDdm(double value, bool longitude, int decimals) { var dir = value < 0 ? (longitude ? 'W' : 'S') : (longitude ? 'E' : 'N'); var abs = Math.Abs(value); return $"{Math.Truncate(abs)}°{((abs % 1) * 60).ToString($"F{decimals}", CultureInfo.InvariantCulture)}′{dir}"; }
    private static string FormatDms(double value, bool longitude, int decimals) { var dir = value < 0 ? (longitude ? 'W' : 'S') : (longitude ? 'E' : 'N'); var abs = Math.Abs(value); var deg = Math.Truncate(abs); var minutes = (abs - deg) * 60; var min = Math.Truncate(minutes); var sec = (minutes - min) * 60; return $"{deg:0}°{min:00}′{sec.ToString($"F{decimals}", CultureInfo.InvariantCulture)}″{dir}"; }
}
