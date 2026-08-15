using UrbanPlanToolbox.Services;
using Windows.Graphics;
using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class RecorderPlacementTests
{
    [Fact]
    public void AutomaticStartUsesThePrimaryWorkAreaTopRightWithPhysicalMargin()
    {
        var point = RecorderPlacement.CalculatePrimaryWorkAreaTopRight(
            new RectInt32(0, 0, 1920, 1040), new SizeInt32(1080, 760));

        Assert.Equal(new PointInt32(820, 20), point);
    }

    [Fact]
    public void PlacementStaysInsideASmallWorkArea()
    {
        var point = RecorderPlacement.CalculatePrimaryWorkAreaTopRight(
            new RectInt32(-1280, 0, 800, 600), new SizeInt32(1080, 760));

        Assert.Equal(new PointInt32(-1280, -160), point);
    }
}
