using Windows.Graphics;
using UrbanPlanToolbox.Services;
using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class WindowPlacementTests
{
    [Fact]
    public void MissingPlacementUsesTheExistingDefaultSize()
    {
        using var scope = new SettingsScope();
        var placement = new WindowPlacementService(new SettingsService(scope.Path)).Load(new SizeInt32(1920, 1080));
        Assert.Equal(new WindowPlacement(1100, 760, false), placement);
    }

    [Theory]
    [InlineData(1400, 900)]
    [InlineData(900, 700)]
    public void RestoredSizeRoundTrips(int width, int height)
    {
        using var scope = new SettingsScope();
        var service = new WindowPlacementService(new SettingsService(scope.Path));
        service.Save(new SizeInt32(width, height), false);
        Assert.Equal(new WindowPlacement(width, height, false), service.Load(new SizeInt32(1920, 1080)));
    }

    [Fact]
    public void MaximizedPreferenceKeepsTheLastNormalRestoreSize()
    {
        using var scope = new SettingsScope();
        var service = new WindowPlacementService(new SettingsService(scope.Path));
        service.Save(new SizeInt32(1300, 800), true);
        Assert.Equal(new WindowPlacement(1300, 800, true), service.Load(new SizeInt32(1920, 1080)));
    }

    [Fact]
    public void InvalidStoredSizeFallsBackToTheDefault()
    {
        using var scope = new SettingsScope();
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(scope.Path)!);
        File.WriteAllText(scope.Path, "{\"LastNormalWindowWidth\":0,\"LastNormalWindowHeight\":50,\"WasWindowMaximized\":true}");
        Assert.Equal(new WindowPlacement(1100, 760, false), new WindowPlacementService(new SettingsService(scope.Path)).Load(new SizeInt32(1920, 1080)));
    }

    [Fact]
    public void PlacementIsClampedToTheCurrentWorkArea()
    {
        var placement = WindowPlacementService.ClampToWorkArea(new WindowPlacement(3000, 2000, false), new SizeInt32(1280, 720));
        Assert.Equal(new WindowPlacement(1280, 720, false), placement);
    }

    private sealed class SettingsScope : IDisposable
    {
        private readonly string _directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"UrbanPlanToolbox-{Guid.NewGuid():N}");
        public string Path => System.IO.Path.Combine(_directory, "settings.json");
        public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true); }
    }
}
