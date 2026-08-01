using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Models.Tools;
using UrbanPlanToolbox.Services;
using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class FavoriteToolsServiceTests
{
    [Fact]
    public void InitialFavoritesAreEmpty()
    {
        WithTempSettings(path =>
        {
            var service = CreateService(path);

            Assert.Empty(service.GetFavoriteTools());
            Assert.False(service.IsFavorite(ToolIds.PlanningIndicatorCalculator));
        });
    }

    [Fact]
    public void AddDuplicateRemoveAndToggleAreSafe()
    {
        WithTempSettings(path =>
        {
            var service = CreateService(path);

            Assert.True(service.Add(ToolIds.PlanningIndicatorCalculator));
            Assert.False(service.Add(ToolIds.PlanningIndicatorCalculator));
            Assert.True(service.IsFavorite(ToolIds.PlanningIndicatorCalculator));
            Assert.False(service.Toggle(ToolIds.PlanningIndicatorCalculator));
            Assert.False(service.IsFavorite(ToolIds.PlanningIndicatorCalculator));
            Assert.True(service.Toggle(ToolIds.UnitScaleConverter));
            Assert.True(service.Remove(ToolIds.UnitScaleConverter));
            Assert.False(service.Remove(ToolIds.UnitScaleConverter));
        });
    }

    [Fact]
    public void FavoritesPersistAndRestoreInRegistryOrder()
    {
        WithTempSettings(path =>
        {
            var first = CreateService(path);
            first.Add(ToolIds.UnitScaleConverter);
            first.Add(ToolIds.PlanningIndicatorCalculator);

            var restored = CreateService(path);

            Assert.Equal(
                [ToolIds.PlanningIndicatorCalculator, ToolIds.UnitScaleConverter],
                restored.GetFavoriteTools().Select(tool => tool.Id));
            var stored = new SettingsService(path).Load();
            Assert.Equal(
                [ToolIds.PlanningIndicatorCalculator, ToolIds.UnitScaleConverter],
                stored.FavoriteToolIds);
        });
    }

    [Fact]
    public void UnknownAndDuplicateStoredIdsAreIgnored()
    {
        WithTempSettings(path =>
        {
            new SettingsService(path).Save(new AppSettings
            {
                FavoriteToolIds =
                [
                    ToolIds.UnitScaleConverter,
                    "removed-tool",
                    ToolIds.UnitScaleConverter,
                    ToolIds.PlanningIndicatorCalculator
                ]
            });

            var service = CreateService(path);

            Assert.Equal(
                [ToolIds.PlanningIndicatorCalculator, ToolIds.UnitScaleConverter],
                service.GetFavoriteTools().Select(tool => tool.Id));
            Assert.False(service.IsFavorite("removed-tool"));
            Assert.False(service.Toggle("removed-tool"));
        });
    }

    [Fact]
    public void CorruptedSettingsFallBackToEmptyFavorites()
    {
        WithTempSettings(path =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "{not-json");

            Assert.Empty(CreateService(path).GetFavoriteTools());
        });
    }

    [Fact]
    public void ChangesRaiseNotificationsOnlyWhenStateChanges()
    {
        WithTempSettings(path =>
        {
            var service = CreateService(path);
            var notifications = 0;
            service.FavoritesChanged += (_, _) => notifications++;

            service.Add(ToolIds.PlanningIndicatorCalculator);
            service.Add(ToolIds.PlanningIndicatorCalculator);
            service.Remove("removed-tool");
            service.Remove(ToolIds.PlanningIndicatorCalculator);

            Assert.Equal(2, notifications);
        });
    }

    [Fact]
    public void UnavailableToolsCannotBecomeFavorites()
    {
        WithTempSettings(path =>
        {
            var registry = new ToolRegistry(
            [
                CreateTool("available", 20, true),
                CreateTool("unavailable", 10, false)
            ]);
            var service = new FavoriteToolsService(new SettingsService(path), registry);

            Assert.False(service.Add("unavailable"));
            Assert.True(service.Add("available"));
            Assert.Equal("available", Assert.Single(service.GetFavoriteTools()).Id);
        });
    }

    private static FavoriteToolsService CreateService(string path) =>
        new(new SettingsService(path), ToolRegistry.Default);

    private static ToolDefinition CreateTool(string id, int sortOrder, bool isAvailable) => new(
        id,
        id,
        id,
        ToolPrimaryCategory.Design,
        ToolSecondaryCategory.PreliminaryAnalysis,
        "\uE10F",
        typeof(Views.PlanningCalculatorPage),
        sortOrder,
        isAvailable,
        id,
        "X",
        [id]);

    private static void WithTempSettings(Action<string> test)
    {
        var folder = Path.Combine(Path.GetTempPath(), $"UrbanPlanToolbox-{Guid.NewGuid():N}");
        var path = Path.Combine(folder, "settings.json");
        try
        {
            test(path);
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }
}
