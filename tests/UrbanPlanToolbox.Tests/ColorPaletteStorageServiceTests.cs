using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Models.Tools;
using UrbanPlanToolbox.Services;
using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class ColorPaletteStorageServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"UrbanPlanToolbox-palette-{Guid.NewGuid():N}");
    private readonly AppDataPathProvider _paths;
    private readonly ColorPaletteStorageService _service;

    public ColorPaletteStorageServiceTests()
    {
        _paths = new AppDataPathProvider(_root, [ToolIds.ColorPaletteRecorder]);
        _service = new ColorPaletteStorageService(_paths);
    }

    [Fact]
    public async Task SchemesRoundTripWithStableIdsAndIndependentSchema()
    {
        var scheme = new ColorPaletteScheme { Name = "Courtyard", Category = ColorPaletteCategories.Warm };
        scheme.Colors.Add(new ColorPaletteColor { Name = "Brick", Hex = "#ab1030", SortOrder = 0 });
        var document = new ColorPaletteDocument { Schemes = [scheme] };

        Assert.True((await _service.SaveAsync(document)).Succeeded);
        var loaded = await _service.ReadAsync();

        Assert.True(loaded.HasValue);
        var restored = Assert.Single(loaded.Value!.Schemes);
        Assert.Equal(scheme.SchemeId, restored.SchemeId);
        Assert.Equal(scheme.Colors[0].ColorId, Assert.Single(restored.Colors).ColorId);
        Assert.Equal("#AB1030", restored.Colors[0].Hex);
        Assert.Contains("\"schemaVersion\": 1", await File.ReadAllTextAsync(_paths.GetToolDataFilePath(ToolIds.ColorPaletteRecorder, ColorPaletteStorageService.DataFileName)));
    }

    [Theory]
    [InlineData("#1a2b3c", "#1A2B3C")]
    [InlineData("1A2B3C", "#1A2B3C")]
    [InlineData("#123", "")]
    [InlineData("#GGGGGG", "")]
    public void HexValuesAreValidatedAndNormalized(string input, string expected)
    {
        var success = ColorPaletteStorageService.TryNormalizeHex(input, out var normalized);
        Assert.Equal(!string.IsNullOrEmpty(expected), success);
        Assert.Equal(expected, normalized);
    }

    [Fact]
    public async Task ImageCopiesUseManagedUniquePathsAndNeverModifySource()
    {
        var source = Path.Combine(_root, "source.png");
        Directory.CreateDirectory(_root);
        await File.WriteAllBytesAsync(source, [1, 2, 3, 4]);
        var scheme = Guid.NewGuid();
        var first = await _service.CopyImageAsync(scheme, source, 0);
        var second = await _service.CopyImageAsync(scheme, source, 1);

        Assert.NotEqual(first.RelativePath, second.RelativePath);
        Assert.Equal([1, 2, 3, 4], await File.ReadAllBytesAsync(source));
        Assert.True(File.Exists(_service.ResolveManagedImagePath(first.RelativePath)));
        _service.DeleteManagedImage(first);
        Assert.True(File.Exists(source));
        Assert.False(File.Exists(_service.ResolveManagedImagePath(first.RelativePath)));
    }

    [Theory]
    [InlineData("../escape.png")]
    [InlineData("C:/escape.png")]
    [InlineData("scheme/../../escape.png")]
    public void UnsafeManagedImagePathsAreRejected(string path) => Assert.Throws<ArgumentException>(() => _service.ResolveManagedImagePath(path));

    [Fact]
    public async Task FutureSchemaIsRejectedWithoutOverwrite()
    {
        var path = _paths.GetToolDataFilePath(ToolIds.ColorPaletteRecorder, ColorPaletteStorageService.DataFileName);
        await File.WriteAllTextAsync(path, "{\"schemaVersion\":2,\"savedAtUtc\":\"2026-01-01T00:00:00+00:00\",\"payload\":{\"schemes\":[]}}");
        var before = await File.ReadAllTextAsync(path);
        var read = await _service.ReadAsync();
        var write = await _service.SaveAsync(new ColorPaletteDocument());
        Assert.Equal(DataStorageStatus.UnsupportedFutureVersion, read.Status);
        Assert.Equal(DataStorageStatus.UnsupportedFutureVersion, write.Status);
        Assert.Equal(before, await File.ReadAllTextAsync(path));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
