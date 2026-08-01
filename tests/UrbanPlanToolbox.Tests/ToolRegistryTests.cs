using UrbanPlanToolbox.Models.Tools;
using UrbanPlanToolbox.Services;
using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class ToolRegistryTests
{
    [Fact]
    public void DefaultRegistryContainsOnlyTheTwoAvailableTools()
    {
        Assert.Collection(
            ToolRegistry.Default.All,
            tool => Assert.Equal(ToolIds.PlanningIndicatorCalculator, tool.Id),
            tool => Assert.Equal(ToolIds.UnitScaleConverter, tool.Id));
        Assert.All(ToolRegistry.Default.All, tool => Assert.True(tool.IsAvailable));
    }

    [Fact]
    public void RegisteredToolIdsAreUnique()
    {
        var ids = ToolRegistry.Default.All.Select(tool => tool.Id).ToArray();

        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [InlineData(ToolIds.PlanningIndicatorCalculator, "规划指标快速计算器")]
    [InlineData(ToolIds.UnitScaleConverter, "单位与比例尺换算器")]
    public void FindsRegisteredToolByStableId(string id, string expectedName)
    {
        Assert.Equal(expectedName, TestLocalization.ZhCn.GetString(ToolRegistry.Default.GetById(id).NameResourceKey));
        Assert.True(ToolRegistry.Default.TryGet(id, out var tool));
        Assert.Equal(id, tool!.Id);
    }

    [Fact]
    public void MissingIdReturnsSafeFailure()
    {
        Assert.False(ToolRegistry.Default.TryGet("removed-tool", out var tool));
        Assert.Null(tool);
        Assert.False(ToolRegistry.Default.TryGet(null, out tool));
        Assert.Null(tool);
    }

    [Fact]
    public void PlanningCalculatorHasMasterPlanningCategory()
    {
        var tool = ToolRegistry.Default.GetById(ToolIds.PlanningIndicatorCalculator);

        Assert.Equal(ToolPrimaryCategory.Design, tool.PrimaryCategory);
        Assert.Equal(ToolSecondaryCategory.MasterPlanning, tool.SecondaryCategory);
        Assert.Equal("Navigation_DesignTools", tool.PrimaryCategory.GetNameResourceKey());
        Assert.Equal("Category_MasterPlanning", tool.SecondaryCategory.GetNameResourceKey());
    }

    [Fact]
    public void UnitScaleConverterHasDetailedDesignCategory()
    {
        var tool = ToolRegistry.Default.GetById(ToolIds.UnitScaleConverter);

        Assert.Equal(ToolPrimaryCategory.Design, tool.PrimaryCategory);
        Assert.Equal(ToolSecondaryCategory.DetailedDesign, tool.SecondaryCategory);
        Assert.Equal("Navigation_DesignTools", tool.PrimaryCategory.GetNameResourceKey());
        Assert.Equal("Category_DetailedDesign", tool.SecondaryCategory.GetNameResourceKey());
    }

    [Fact]
    public void ToolsMapToTheirExistingPageTypes()
    {
        Assert.Equal(typeof(Views.PlanningCalculatorPage), ToolRegistry.Default.GetById(ToolIds.PlanningIndicatorCalculator).PageType);
        Assert.Equal(typeof(Views.UnitScaleConverterPage), ToolRegistry.Default.GetById(ToolIds.UnitScaleConverter).PageType);
    }

    [Fact]
    public void CategoryFiltersReturnMatchingTools()
    {
        Assert.Equal(2, ToolRegistry.Default.GetByPrimaryCategory(ToolPrimaryCategory.Design).Count);
        Assert.Empty(ToolRegistry.Default.GetByPrimaryCategory(ToolPrimaryCategory.Research));
        Assert.Equal(
            [ToolIds.PlanningIndicatorCalculator, ToolIds.UnitScaleConverter],
            ToolRegistry.Default.GetAvailableByPrimaryCategory(ToolPrimaryCategory.Design).Select(tool => tool.Id));
        Assert.Empty(ToolRegistry.Default.GetAvailableByPrimaryCategory(ToolPrimaryCategory.Research));
        Assert.Equal(
            ToolIds.PlanningIndicatorCalculator,
            Assert.Single(ToolRegistry.Default.GetBySecondaryCategory(ToolSecondaryCategory.MasterPlanning)).Id);
        Assert.Equal(
            ToolIds.UnitScaleConverter,
            Assert.Single(ToolRegistry.Default.GetBySecondaryCategory(ToolSecondaryCategory.DetailedDesign)).Id);
    }

    [Fact]
    public void CategoryCatalogUsesUniqueStableIdsInRoadmapOrder()
    {
        Assert.Equal(
            ["preliminary-analysis", "field-research", "design-development", "master-planning", "detailed-design"],
            ToolCategoryCatalog.Design.Select(category => category.Id));
        Assert.Equal(
            ["research-preparation", "geographic-tools", "data-tools"],
            ToolCategoryCatalog.Research.Select(category => category.Id));

        var allIds = ToolCategoryCatalog.Design.Concat(ToolCategoryCatalog.Research).Select(category => category.Id).ToArray();
        Assert.Equal(allIds.Length, allIds.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal([10, 20, 30, 40, 50], ToolCategoryCatalog.Design.Select(category => category.SortOrder));
        Assert.Equal([10, 20, 30], ToolCategoryCatalog.Research.Select(category => category.SortOrder));
    }

    [Fact]
    public void CategoryCatalogSafelyHandlesMissingIds()
    {
        Assert.True(ToolCategoryCatalog.TryGet("master-planning", out var category));
        Assert.Equal(ToolSecondaryCategory.MasterPlanning, category!.SecondaryCategory);
        Assert.False(ToolCategoryCatalog.TryGet("missing-category", out category));
        Assert.Null(category);
        Assert.False(ToolCategoryCatalog.TryGet(null, out category));
        Assert.Null(category);
    }

    [Fact]
    public void CombinedCategoryFilterReturnsOnlyTheExpectedDesignTools()
    {
        Assert.Equal(
            ToolIds.PlanningIndicatorCalculator,
            Assert.Single(ToolRegistry.Default.GetAvailableByCategories(
                ToolPrimaryCategory.Design,
                ToolSecondaryCategory.MasterPlanning)).Id);
        Assert.Equal(
            ToolIds.UnitScaleConverter,
            Assert.Single(ToolRegistry.Default.GetAvailableByCategories(
                ToolPrimaryCategory.Design,
                ToolSecondaryCategory.DetailedDesign)).Id);

        Assert.Empty(ToolRegistry.Default.GetAvailableByCategories(ToolPrimaryCategory.Design, ToolSecondaryCategory.PreliminaryAnalysis));
        Assert.Empty(ToolRegistry.Default.GetAvailableByCategories(ToolPrimaryCategory.Design, ToolSecondaryCategory.FieldResearch));
        Assert.Empty(ToolRegistry.Default.GetAvailableByCategories(ToolPrimaryCategory.Design, ToolSecondaryCategory.DesignDevelopment));
    }

    [Fact]
    public void ResearchCategoriesCurrentlyContainNoAvailableTools()
    {
        foreach (var category in ToolCategoryCatalog.Research)
        {
            Assert.Empty(ToolRegistry.Default.GetAvailableByCategories(
                ToolPrimaryCategory.Research,
                category.SecondaryCategory));
        }
    }

    [Fact]
    public void InvalidCategoryCombinationReturnsNoTools()
    {
        Assert.Empty(ToolRegistry.Default.GetAvailableByCategories(
            ToolPrimaryCategory.Design,
            ToolSecondaryCategory.ResearchPreparation));
        Assert.Empty(ToolRegistry.Default.GetAvailableByCategories(
            ToolPrimaryCategory.Design,
            (ToolSecondaryCategory)999));
    }

    [Fact]
    public void ToolCardMetadataComesFromRegisteredDefinitions()
    {
        var masterPlanningCard = Assert.Single(ToolRegistry.Default.GetAvailableByCategories(
            ToolPrimaryCategory.Design,
            ToolSecondaryCategory.MasterPlanning));
        var detailedDesignCard = Assert.Single(ToolRegistry.Default.GetAvailableByCategories(
            ToolPrimaryCategory.Design,
            ToolSecondaryCategory.DetailedDesign));

        Assert.Equal(ToolIds.PlanningIndicatorCalculator, masterPlanningCard.Id);
        Assert.Equal("规划指标快速计算器", TestLocalization.ZhCn.GetString(masterPlanningCard.NameResourceKey));
        Assert.False(string.IsNullOrWhiteSpace(TestLocalization.ZhCn.GetString(masterPlanningCard.DescriptionResourceKey)));
        Assert.False(string.IsNullOrWhiteSpace(masterPlanningCard.IconGlyph));
        Assert.Equal(typeof(Views.PlanningCalculatorPage), masterPlanningCard.PageType);

        Assert.Equal(ToolIds.UnitScaleConverter, detailedDesignCard.Id);
        Assert.Equal("单位与比例尺换算器", TestLocalization.ZhCn.GetString(detailedDesignCard.NameResourceKey));
        Assert.False(string.IsNullOrWhiteSpace(TestLocalization.ZhCn.GetString(detailedDesignCard.DescriptionResourceKey)));
        Assert.False(string.IsNullOrWhiteSpace(detailedDesignCard.IconGlyph));
        Assert.Equal(typeof(Views.UnitScaleConverterPage), detailedDesignCard.PageType);
    }

    [Fact]
    public void RegistryUsesStableSortOrder()
    {
        var registry = new ToolRegistry(
        [
            CreateTool("second", 20),
            CreateTool("first-b", 10),
            CreateTool("first-a", 10)
        ]);

        Assert.Equal(["first-a", "first-b", "second"], registry.All.Select(tool => tool.Id));
    }

    [Fact]
    public void DuplicateToolIdIsRejected()
    {
        var tools = new[] { CreateTool("duplicate", 10), CreateTool("duplicate", 20) };

        Assert.Throws<ArgumentException>(() => new ToolRegistry(tools));
    }

    private static ToolDefinition CreateTool(string id, int sortOrder) => new(
        id,
        $"{id}_Name",
        $"{id}_Description",
        ToolPrimaryCategory.Design,
        ToolSecondaryCategory.PreliminaryAnalysis,
        "\uE10F",
        typeof(Views.PlanningCalculatorPage),
        sortOrder,
        true,
        id,
        "X",
        $"{id}_Keywords");
}
