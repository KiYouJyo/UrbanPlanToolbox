using Windows.Graphics;

namespace UrbanPlanToolbox.Services;

/// <summary>Calculates the default automatic-start position in AppWindow physical pixels.</summary>
public static class RecorderPlacement
{
    public const int AutomaticStartMargin = 20;

    public static PointInt32 CalculatePrimaryWorkAreaTopRight(RectInt32 workArea, SizeInt32 windowSize)
    {
        var x = Math.Max(workArea.X, workArea.X + workArea.Width - windowSize.Width - AutomaticStartMargin);
        var y = Math.Min(workArea.Y + workArea.Height - windowSize.Height, workArea.Y + AutomaticStartMargin);
        return new PointInt32(x, y);
    }
}
