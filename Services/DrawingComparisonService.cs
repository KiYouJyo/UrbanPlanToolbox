using OpenCvSharp;
using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.Services;

public sealed class DrawingComparisonService
{
    public DrawingSizeValidationResult ValidateSameSize(Mat versionA, Mat versionB)
    {
        ArgumentNullException.ThrowIfNull(versionA);
        ArgumentNullException.ThrowIfNull(versionB);
        return new(!versionA.Empty() && !versionB.Empty() && versionA.Width == versionB.Width && versionA.Height == versionB.Height,
            versionA.Width, versionA.Height, versionB.Width, versionB.Height);
    }

    public Mat CreateOverlay(Mat versionA, Mat versionB, double opacity)
    {
        EnsureSameSize(versionA, versionB);
        var result = new Mat(); Cv2.AddWeighted(versionA, 1 - Math.Clamp(opacity, 0, 1), versionB, Math.Clamp(opacity, 0, 1), 0, result); return result;
    }

    public static void EnsureSameSize(Mat versionA, Mat versionB)
    {
        if (versionA.Empty() || versionB.Empty() || versionA.Width != versionB.Width || versionA.Height != versionB.Height)
            throw new InvalidDataException("Image dimensions must match before comparison.");
    }
}
