using UrbanPlanToolbox.Models;
using Windows.Storage;

namespace UrbanPlanToolbox.Services;

public sealed class FieldSurveyPhotoImportService(FieldSurveyPhotoMetadataService? metadata = null, AppLogger? logger = null)
{
    private static readonly HashSet<string> Extensions = new([".jpg", ".jpeg", ".heic", ".heif", ".png"], StringComparer.OrdinalIgnoreCase);
    private readonly FieldSurveyPhotoMetadataService _metadata = metadata ?? new();
    private readonly AppLogger _logger = logger ?? AppLogger.Default;

    public async Task<PhotoImportResult> ImportAsync(IEnumerable<string> paths, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        var photos = new List<FieldSurveyPhoto>(); var unsupported = new List<string>(); var failed = new List<string>(); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var duplicate = 0; var count = 0; var candidates = paths.ToArray();
        _logger.Info("FieldSurveyPhoto", "PhotoImportStarted", $"count={candidates.Length}");
        foreach (var path in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested(); count++;
            var full = Path.GetFullPath(path);
            if (!Extensions.Contains(Path.GetExtension(full))) { unsupported.Add(full); progress?.Report(count); continue; }
            if (!seen.Add(full)) { duplicate++; progress?.Report(count); continue; }
            try { var info = await Task.Run(() => _metadata.Read(full), cancellationToken); photos.Add(new FieldSurveyPhoto { SourcePath = full, CapturedAt = info.CapturedAt, Longitude = info.Longitude, Latitude = info.Latitude, Altitude = info.Altitude, Heading = info.Heading, Make = info.Make, Model = info.Model, Orientation = info.Orientation, GpsStatus = info.GpsStatus }); }
            catch (Exception ex) { failed.Add(full); _logger.Warning("FieldSurveyPhoto", "ExifReadFailed", ex.GetType().Name); }
            progress?.Report(count);
        }
        _logger.Info("FieldSurveyPhoto", failed.Count == 0 && unsupported.Count == 0 ? "PhotoImportSucceeded" : "PhotoImportPartialFailure", $"success={photos.Count};failed={failed.Count};unsupported={unsupported.Count};duplicates={duplicate};gps={photos.Count(p => p.GpsStatus == PhotoGpsStatus.Valid)}");
        return new(photos, unsupported, failed, duplicate);
    }

    public async Task<PhotoImportResult> ImportAsync(IEnumerable<StorageFile> files, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        var photos = new List<FieldSurveyPhoto>(); var unsupported = new List<string>(); var failed = new List<string>(); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var duplicate = 0; var count = 0; var candidates = files.ToArray();
        _logger.Info("FieldSurveyPhoto", "PhotoImportStarted", $"count={candidates.Length}");
        foreach (var file in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested(); count++;
            var sourcePath = file.Path;
            var extension = Path.GetExtension(file.Name);
            if (!Extensions.Contains(extension) && !Extensions.Contains(file.FileType)) { unsupported.Add(extension.Length == 0 ? file.FileType : extension); progress?.Report(count); continue; }
            if (!seen.Add(sourcePath)) { duplicate++; progress?.Report(count); continue; }
            try
            {
                using var stream = await file.OpenStreamForReadAsync();
                var info = _metadata.Read(stream);
                photos.Add(new FieldSurveyPhoto { SourcePath = sourcePath, CapturedAt = info.CapturedAt, Longitude = info.Longitude, Latitude = info.Latitude, Altitude = info.Altitude, Heading = info.Heading, Make = info.Make, Model = info.Model, Orientation = info.Orientation, GpsStatus = info.GpsStatus });
            }
            catch (Exception ex) { failed.Add(file.FileType); _logger.Warning("FieldSurveyPhoto", "PhotoImportFailed", ex.GetType().Name); }
            progress?.Report(count);
        }
        _logger.Info("FieldSurveyPhoto", failed.Count == 0 && unsupported.Count == 0 ? "PhotoImportSucceeded" : "PhotoImportPartialFailure", $"success={photos.Count};failed={failed.Count};unsupported={unsupported.Count};duplicates={duplicate};gps={photos.Count(p => p.GpsStatus == PhotoGpsStatus.Valid)}");
        return new(photos, unsupported, failed, duplicate);
    }
}
