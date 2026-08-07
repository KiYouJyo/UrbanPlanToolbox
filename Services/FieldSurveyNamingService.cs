using System.Globalization;
using System.Text;
using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.Services;

public sealed class FieldSurveyNamingService
{
    public void AssignIds(IList<FieldSurveyPhoto> photos) { var width = Math.Max(3, photos.Count.ToString(CultureInfo.InvariantCulture).Length); for (var i = 0; i < photos.Count; i++) photos[i].Id = $"P{(i + 1).ToString($"D{width}", CultureInfo.InvariantCulture)}"; }
    public string BuildFileName(FieldSurveyPhoto photo, string template)
    {
        var date = photo.CapturedAt?.ToLocalTime().ToString("yyyyMMdd", CultureInfo.InvariantCulture) ?? "undated"; var time = photo.CapturedAt?.ToLocalTime().ToString("HHmmss", CultureInfo.InvariantCulture) ?? "000000";
        var result = template.Replace("{ID}", photo.Id, StringComparison.OrdinalIgnoreCase).Replace("{Date}", date, StringComparison.OrdinalIgnoreCase).Replace("{Time}", time, StringComparison.OrdinalIgnoreCase).Replace("{OriginalName}", Path.GetFileNameWithoutExtension(photo.OriginalName), StringComparison.OrdinalIgnoreCase);
        return Sanitize(result) + Path.GetExtension(photo.OriginalName).ToLowerInvariant();
    }
    public static string Sanitize(string value) { var invalid = Path.GetInvalidFileNameChars(); var builder = new StringBuilder(value.Length); foreach (var c in value) builder.Append(invalid.Contains(c) ? '_' : c); var result = builder.ToString().Trim().TrimEnd('.'); return string.IsNullOrWhiteSpace(result) ? "photo" : result; }
}
