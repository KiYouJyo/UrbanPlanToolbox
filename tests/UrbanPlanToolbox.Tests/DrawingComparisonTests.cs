using OpenCvSharp;
using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Services;
using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class DrawingComparisonTests
{
    [Fact]
    public void DifferenceAnalysisFindsAndFiltersConnectedRegions()
    {
        using var a = new Mat(120, 160, MatType.CV_8UC3, Scalar.White); using var b = a.Clone();
        Cv2.Rectangle(b, new Rect(20, 20, 20, 20), Scalar.Black, -1); Cv2.Rectangle(b, new Rect(100, 80, 2, 2), Scalar.Black, -1);
        var result = new DifferenceAnalysisService().Analyze(a, b, 10, 10);
        Assert.True(result.Succeeded); Assert.Single(result.Regions); Assert.True(result.Regions[0].Area >= 400); Assert.NotEmpty(result.MaskPng); Assert.NotEmpty(result.ResultPng);
    }

    [Fact]
    public void SameSizeImagesAreAllowedAndMismatchedImagesAreRejected()
    {
        using var a = new Mat(100, 120, MatType.CV_8UC3, Scalar.White); using var b = new Mat(100, 120, MatType.CV_8UC3, Scalar.White); using var wrong = new Mat(101, 120, MatType.CV_8UC3, Scalar.White);
        var service = new DrawingComparisonService();
        Assert.True(service.ValidateSameSize(a, b).IsValid); Assert.False(service.ValidateSameSize(a, wrong).IsValid);
        Assert.False(new DifferenceAnalysisService().Analyze(a, wrong).Succeeded);
    }

    [Fact]
    public void DifferenceCancellationIsHonored()
    {
        using var blank = new Mat(100, 100, MatType.CV_8UC3, Scalar.White);
        using var cts = new CancellationTokenSource(); cts.Cancel(); Assert.Throws<OperationCanceledException>(() => new DifferenceAnalysisService().Analyze(blank, blank, cancellationToken: cts.Token));
    }

    [Fact]
    public void OverlayUsesRequestedOpacity()
    {
        using var a = new Mat(4, 4, MatType.CV_8UC3, Scalar.Black); using var b = new Mat(4, 4, MatType.CV_8UC3, Scalar.White); using var overlay = new DrawingComparisonService().CreateOverlay(a, b, .5);
        Assert.InRange(overlay.Get<Vec3b>(0, 0).Item0, (byte)127, (byte)128);
    }

    [Fact]
    public async Task ImageLoaderRejectsUnsupportedAndLoadsPng()
    {
        var root = Path.Combine(Path.GetTempPath(), "UrbanPlanToolboxDrawingTests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); var png = Path.Combine(root, "a.png");
        using (var image = new Mat(10, 10, MatType.CV_8UC3, Scalar.White)) Cv2.ImWrite(png, image);
        using var loaded = await new DrawingLoadService().LoadAsync(new(png)); Assert.False(loaded.Empty()); var unsupported = Path.Combine(root, "bad.tif"); await File.WriteAllTextAsync(unsupported, "not an image"); await Assert.ThrowsAsync<NotSupportedException>(() => new DrawingLoadService().LoadAsync(new(unsupported)));
        Directory.Delete(root, true);
    }
}
