using OpenCvSharp;
using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.Services;

public sealed class DifferenceAnalysisService
{
    public Task<DifferenceResult> AnalyzeAsync(Mat versionA, Mat versionB, double sensitivity = 30, int minimumArea = 20, CancellationToken cancellationToken = default) =>
        Task.Run(() => Analyze(versionA, versionB, sensitivity, minimumArea, cancellationToken), cancellationToken);

    public DifferenceResult Analyze(Mat versionA, Mat versionB, double sensitivity = 30, int minimumArea = 20, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested(); if (versionA.Empty() || versionB.Empty()) throw new InvalidDataException("Images cannot be empty.");
            if (versionA.Size() != versionB.Size()) throw new InvalidDataException("Image dimensions must match before comparison.");
            using var a = versionA.Clone(); using var b = versionB.Clone();
            using var ga = new Mat(); using var gb = new Mat(); Cv2.CvtColor(a, ga, ColorConversionCodes.BGR2GRAY); Cv2.CvtColor(b, gb, ColorConversionCodes.BGR2GRAY);
            using var diff = new Mat(); Cv2.Absdiff(ga, gb, diff); using var mask = new Mat(); Cv2.Threshold(diff, mask, Math.Clamp(sensitivity, 1, 254), 255, ThresholdTypes.Binary);
            using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(3, 3)); Cv2.MorphologyEx(mask, mask, MorphTypes.Open, kernel);
            using var labels = new Mat(); using var stats = new Mat(); using var centroids = new Mat(); Cv2.ConnectedComponentsWithStats(mask, labels, stats, centroids, PixelConnectivity.Connectivity8, MatType.CV_32S);
            var regions = new List<DifferenceRegion>(); using var visible = new Mat(mask.Size(), MatType.CV_8UC3, Scalar.Black); var added = 0; var deleted = 0;
            for (var i = 1; i < stats.Rows; i++) { var area = stats.Get<int>(i, (int)ConnectedComponentsTypes.Area); if (area < Math.Max(1, minimumArea)) continue; var x = stats.Get<int>(i, (int)ConnectedComponentsTypes.Left); var y = stats.Get<int>(i, (int)ConnectedComponentsTypes.Top); var w = stats.Get<int>(i, (int)ConnectedComponentsTypes.Width); var h = stats.Get<int>(i, (int)ConnectedComponentsTypes.Height); regions.Add(new(i, x, y, w, h, area)); Cv2.Rectangle(visible, new Rect(x, y, w, h), Scalar.Red, 2); }
            var result = new Mat(a.Size(), MatType.CV_8UC3, Scalar.Black); using var bGray = gb.Clone(); using var aOnly = new Mat(); using var bOnly = new Mat(); Cv2.Threshold(ga - gb, aOnly, Math.Clamp(sensitivity, 1, 254), 255, ThresholdTypes.Binary); Cv2.Threshold(gb - ga, bOnly, Math.Clamp(sensitivity, 1, 254), 255, ThresholdTypes.Binary); using var dim = new Mat(); Cv2.CvtColor(a, dim, ColorConversionCodes.BGR2BGRA); Cv2.ConvertScaleAbs(a, dim, .35, 0); dim.CopyTo(result); result.SetTo(new Scalar(255, 0, 0), aOnly); result.SetTo(new Scalar(0, 0, 255), bOnly);
            Cv2.ImEncode(".png", mask, out var maskPng); Cv2.ImEncode(".png", result, out var resultPng);
            return new(true, maskPng, resultPng, regions, added, deleted);
        }
        catch (OperationCanceledException) { throw; } catch (Exception ex) { AppLogger.Default.Error(nameof(DifferenceAnalysisService), "DifferenceAnalysisFailed", ex); return new(false, [], [], [], 0, 0, ex.Message); }
    }
}
