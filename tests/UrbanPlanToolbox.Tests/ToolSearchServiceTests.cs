using UrbanPlanToolbox.Models.Tools;
using UrbanPlanToolbox.Services;
using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class ToolSearchServiceTests
{
    private readonly ToolSearchService _service = new(ToolRegistry.Default, TestLocalization.ZhCn);

    [Fact]
    public void RegisteredToolsHaveStableSearchMetadata()
    {
        var calculator = ToolRegistry.Default.GetById(ToolIds.PlanningIndicatorCalculator);
        var converter = ToolRegistry.Default.GetById(ToolIds.UnitScaleConverter);

        Assert.Equal("guihuazhibiaokuaisujisuanqi", calculator.PinyinSortKey);
        Assert.Equal("G", calculator.PinyinInitial);
        Assert.Equal("danweiyubilichihuansuanqi", converter.PinyinSortKey);
        Assert.Equal("D", converter.PinyinInitial);
        Assert.Equal("S", ToolRegistry.Default.GetById(ToolIds.ColorPaletteRecorder).PinyinInitial);
        Assert.All(ToolRegistry.Default.All, tool =>
        {
            Assert.False(string.IsNullOrWhiteSpace(tool.SearchKeywordsResourceKey));
            Assert.NotEmpty(
                TestLocalization.ZhCn.GetString(tool.SearchKeywordsResourceKey)
                    .Split(['\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        });
    }

    [Theory]
    [InlineData("规划", ToolIds.PlanningIndicatorCalculator)]
    [InlineData("容积率", ToolIds.PlanningIndicatorCalculator)]
    [InlineData("guihuazhibiao", ToolIds.PlanningIndicatorCalculator)]
    [InlineData("ksjsq", ToolIds.PlanningIndicatorCalculator)]
    [InlineData("GHZBKSJSQ", ToolIds.PlanningIndicatorCalculator)]
    [InlineData("planning-indicator-calculator", ToolIds.PlanningIndicatorCalculator)]
    [InlineData("单位", ToolIds.UnitScaleConverter)]
    [InlineData("dwyblchhq", ToolIds.UnitScaleConverter)]
    [InlineData("色卡", ToolIds.ColorPaletteRecorder)]
    [InlineData("color-palette-recorder", ToolIds.ColorPaletteRecorder)]
    public void SearchMatchesConfiguredChineseAndPinyinFields(string query, string expectedId)
    {
        var results = Flatten(_service.Search(query, _ => false)).ToArray();
        if (query == "规划") Assert.Contains(expectedId, results.Select(tool => tool.Id));
        else Assert.Equal(expectedId, Assert.Single(results).Id);
    }

    [Fact]
    public void SearchIgnoresOuterWhitespaceAndReturnsEmptyForNoMatch()
    {
        Assert.Equal(
            Flatten(_service.Search("单位", _ => false)).Select(tool => tool.Id),
            Flatten(_service.Search("  单位  ", _ => false)).Select(tool => tool.Id));
        Assert.Empty(Flatten(_service.Search("not-a-tool", _ => false)));
    }

    [Fact]
    public void EmptySearchGroupsAvailableToolsByPinyinInitial()
    {
        var groups = _service.Search(" ", _ => false);

        Assert.Equal(["D", "G", "K", "L", "P", "S", "T", "X", "Z"], groups.Select(group => group.Header));
        Assert.Equal([ToolIds.UnitScaleConverter, ToolIds.DesignConceptDictionary], groups[0].Tools.Select(tool => tool.Id));
        Assert.Equal(ToolIds.PlanningIndicatorCalculator, Assert.Single(groups[1].Tools).Id);
    }

    [Fact]
    public void FavoritesArePinnedAndNeverDuplicatedInLetterGroups()
    {
        var groups = _service.Search(string.Empty, tool => tool.Id == ToolIds.PlanningIndicatorCalculator);

        Assert.Equal("已收藏", groups[0].Header);
        Assert.Equal(ToolIds.PlanningIndicatorCalculator, Assert.Single(groups[0].Tools).Id);
        Assert.Equal(["D", "K", "L", "P", "S", "T", "X", "Z"], groups.Skip(1).Select(group => group.Header));
        Assert.DoesNotContain(groups.Skip(1).SelectMany(group => group.Tools), tool => tool.Id == ToolIds.PlanningIndicatorCalculator);
    }

    [Fact]
    public void InvalidInitialUsesHashGroupAndUnavailableToolsAreExcluded()
    {
        var registry = new ToolRegistry(
        [
            CreateTool("hash", "zeta", "?", true),
            CreateTool("hidden", "alpha", "A", false)
        ]);
        var service = new ToolSearchService(registry, TestLocalization.ZhCn);

        var group = Assert.Single(service.Search(string.Empty, _ => false));
        Assert.Equal("#", group.Header);
        Assert.Equal("hash", Assert.Single(group.Tools).Id);
    }

    private static IEnumerable<LocalizedTool> Flatten(IReadOnlyList<ToolSearchGroup> groups) =>
        groups.SelectMany(group => group.Tools);

    private static ToolDefinition CreateTool(string id, string sortKey, string initial, bool available) => new(
        id,
        $"{id}_Name",
        $"{id}_Description",
        ToolPrimaryCategory.Design,
        ToolSecondaryCategory.PreliminaryAnalysis,
        "\uE10F",
        typeof(Views.PlanningCalculatorPage),
        10,
        available,
        sortKey,
        initial,
        $"{id}_Keywords");
}
