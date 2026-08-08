using OpenCvSharp;
using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.Services;

public sealed class DrawingExportService
{
    public ComparisonExportResult ExportPng(Mat image, string requestedPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(requestedPath)) throw new ArgumentException("An output path is required.", nameof(requestedPath));
            var path = Path.GetFullPath(requestedPath); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            if (!Cv2.ImWrite(path, image)) throw new IOException("OpenCV could not write the PNG.");
            return new(true, path);
        }
        catch (Exception ex) { AppLogger.Default.Error(nameof(DrawingExportService), "ExportFailed", ex); return new(false, null, "Export failed."); }
    }
}
