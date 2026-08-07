using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Services;
using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class FieldSurveyPhotoTests
{
    [Fact] public void NamingSanitizesUserValuesAndPreservesExtension() { var photo = new FieldSurveyPhoto { SourcePath = "C:/IMG.JPG", Id = "P001" }; var name = new FieldSurveyNamingService().BuildFileName(photo, "{ID}_{OriginalName}"); Assert.Equal("P001_IMG.jpg", name); }
    [Fact] public void IdWidthIsConsistent() { var photos = Enumerable.Range(0, 12).Select(_ => new FieldSurveyPhoto { SourcePath = Guid.NewGuid() + ".jpg" }).ToList(); new FieldSurveyNamingService().AssignIds(photos); Assert.Equal("P001", photos[0].Id); Assert.Equal("P012", photos[^1].Id); }
    [Fact] public void InvalidGpsIsNotExportableAsPoint() { var photo = new FieldSurveyPhoto { SourcePath = "x.jpg", GpsStatus = PhotoGpsStatus.NoGps }; Assert.NotEqual(PhotoGpsStatus.Valid, photo.GpsStatus); }
    [Fact] public void TagsNormalizeCommaAndSemicolonInput() { var tags = "步行环境, 违停；步行环境".Split([',', '，', ';', '；'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(); Assert.Equal(["步行环境", "违停"], tags); }
    [Fact] public async Task UppercaseHeicIsProcessedInsteadOfSilentlyUnsupported() { var path = Path.Combine(Path.GetTempPath(), $"UrbanPlanToolbox-{Guid.NewGuid():N}.HEIC"); await File.WriteAllBytesAsync(path, [0, 1, 2]); try { var result = await new FieldSurveyPhotoImportService(new FieldSurveyPhotoMetadataService(), new AppLogger(Path.Combine(Path.GetTempPath(), "UrbanPlanToolbox-test-logs"))).ImportAsync([path]); Assert.Single(result.Photos); Assert.Empty(result.UnsupportedFiles); } finally { File.Delete(path); } }
}
