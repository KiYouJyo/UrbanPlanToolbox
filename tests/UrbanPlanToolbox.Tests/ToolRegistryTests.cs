using UrbanPlanToolbox.Models.Tools;
using UrbanPlanToolbox.Services;
using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class ToolRegistryTests
{
    [Fact]
    public void DefaultRegistryContainsTheAvailableTools()
    {
        Assert.Collection(
            ToolRegistry.Default.All,
            tool => Assert.Equal(ToolIds.PlanningIndicatorCalculator, tool.Id),
            tool => Assert.Equal(ToolIds.UnitScaleConverter, tool.Id),
            tool => Assert.Equal(ToolIds.ColorPaletteRecorder, tool.Id),
            tool => Assert.Equal(ToolIds.WorkflowReviewChecklist, tool.Id),
            tool => Assert.Equal(ToolIds.RegulationsIndex, tool.Id),
            tool => Assert.Equal(ToolIds.DesignConceptDictionary, tool.Id));
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
    public void ColorPaletteRecorderHasDetailedDesignCategoryAndPage()
    {
        var tool = ToolRegistry.Default.GetById(ToolIds.ColorPaletteRecorder);
        Assert.Equal(ToolPrimaryCategory.Design, tool.PrimaryCategory);
        Assert.Equal(ToolSecondaryCategory.DetailedDesign, tool.SecondaryCategory);
        Assert.Equal(typeof(Views.ColorPaletteRecorderPage), tool.PageType);
    }

    [Fact]
    public void DesignConceptDictionaryHasDesignDevelopmentCategoryAndPage()
    {
        var tool = ToolRegistry.Default.GetById(ToolIds.DesignConceptDictionary);

        Assert.Equal(ToolPrimaryCategory.Design, tool.PrimaryCategory);
        Assert.Equal(ToolSecondaryCategory.DesignDevelopment, tool.SecondaryCategory);
        Assert.Equal(typeof(Views.DesignConceptDictionaryPage), tool.PageType);
        Assert.Single(tool.CategoryPlacements);
    }

    [Fact]
    public void ToolsMapToTheirExistingPageTypes()
    {
        Assert.Equal(typeof(Views.PlanningCalculatorPage), ToolRegistry.Default.GetById(ToolIds.PlanningIndicatorCalculator).PageType);
        Assert.Equal(typeof(Views.UnitScaleConverterPage), ToolRegistry.Default.GetById(ToolIds.UnitScaleConverter).PageType);
        Assert.Equal(typeof(Views.ColorPaletteRecorderPage), ToolRegistry.Default.GetById(ToolIds.ColorPaletteRecorder).PageType);
    }

    [Fact]
    public void CategoryFiltersReturnMatchingTools()
    {
        Assert.Equal(6, ToolRegistry.Default.GetByPrimaryCategory(ToolPrimaryCategory.Design).Count);
        Assert.Equal([ToolIds.ColorPaletteRecorder, ToolIds.WorkflowReviewChecklist, ToolIds.RegulationsIndex], ToolRegistry.Default.GetByPrimaryCategory(ToolPrimaryCategory.Research).Select(tool => tool.Id));
        Assert.Equal(
            [ToolIds.PlanningIndicatorCalculator, ToolIds.UnitScaleConverter, ToolIds.ColorPaletteRecorder, ToolIds.WorkflowReviewChecklist, ToolIds.RegulationsIndex, ToolIds.DesignConceptDictionary],
            ToolRegistry.Default.GetAvailableByPrimaryCategory(ToolPrimaryCategory.Design).Select(tool => tool.Id));
        Assert.Equal([ToolIds.ColorPaletteRecorder, ToolIds.WorkflowReviewChecklist, ToolIds.RegulationsIndex], ToolRegistry.Default.GetAvailableByPrimaryCategory(ToolPrimaryCategory.Research).Select(tool => tool.Id));
        Assert.Equal(
            [ToolIds.PlanningIndicatorCalculator, ToolIds.RegulationsIndex],
            ToolRegistry.Default.GetBySecondaryCategory(ToolSecondaryCategory.MasterPlanning).Select(tool => tool.Id));
        Assert.Equal([ToolIds.UnitScaleConverter, ToolIds.ColorPaletteRecorder], ToolRegistry.Default.GetBySecondaryCategory(ToolSecondaryCategory.DetailedDesign).Select(tool => tool.Id));
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
            [ToolIds.PlanningIndicatorCalculator, ToolIds.RegulationsIndex],
            ToolRegistry.Default.GetAvailableByCategories(ToolPrimaryCategory.Design, ToolSecondaryCategory.MasterPlanning).Select(tool => tool.Id));
        Assert.Equal([ToolIds.UnitScaleConverter, ToolIds.ColorPaletteRecorder], ToolRegistry.Default.GetAvailableByCategories(
                ToolPrimaryCategory.Design, ToolSecondaryCategory.DetailedDesign).Select(tool => tool.Id));

        Assert.Equal(ToolIds.WorkflowReviewChecklist, Assert.Single(ToolRegistry.Default.GetAvailableByCategories(ToolPrimaryCategory.Design, ToolSecondaryCategory.PreliminaryAnalysis)).Id);
        Assert.Empty(ToolRegistry.Default.GetAvailableByCategories(ToolPrimaryCategory.Design, ToolSecondaryCategory.FieldResearch));
        Assert.Equal([ToolIds.DesignConceptDictionary], ToolRegistry.Default.GetAvailableByCategories(ToolPrimaryCategory.Design, ToolSecondaryCategory.DesignDevelopment).Select(tool => tool.Id));
    }

    [Fact]
    public void ResearchCategoriesExposeSharedToolsWithoutDuplicatingStableIds()
    {
        foreach (var category in ToolCategoryCatalog.Research)
        {
            var tools = ToolRegistry.Default.GetAvailableByCategories(ToolPrimaryCategory.Research, category.SecondaryCategory);
            if (category.SecondaryCategory == ToolSecondaryCategory.ResearchPreparation)
                Assert.Equal([ToolIds.ColorPaletteRecorder, ToolIds.WorkflowReviewChecklist, ToolIds.RegulationsIndex], tools.Select(tool => tool.Id));
            else Assert.Empty(tools);
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
        var masterPlanningCard = ToolRegistry.Default.GetAvailableByCategories(ToolPrimaryCategory.Design, ToolSecondaryCategory.MasterPlanning).Single(tool => tool.Id == ToolIds.PlanningIndicatorCalculator);
        var detailedDesignCards = ToolRegistry.Default.GetAvailableByCategories(
            ToolPrimaryCategory.Design,
            ToolSecondaryCategory.DetailedDesign);

        Assert.Equal(ToolIds.PlanningIndicatorCalculator, masterPlanningCard.Id);
        Assert.Equal("规划指标快速计算器", TestLocalization.ZhCn.GetString(masterPlanningCard.NameResourceKey));
        Assert.False(string.IsNullOrWhiteSpace(TestLocalization.ZhCn.GetString(masterPlanningCard.DescriptionResourceKey)));
        Assert.False(string.IsNullOrWhiteSpace(masterPlanningCard.IconGlyph));
        Assert.Equal(typeof(Views.PlanningCalculatorPage), masterPlanningCard.PageType);

        Assert.Equal([ToolIds.UnitScaleConverter, ToolIds.ColorPaletteRecorder], detailedDesignCards.Select(card => card.Id));
        Assert.All(detailedDesignCards, card => Assert.False(string.IsNullOrWhiteSpace(TestLocalization.ZhCn.GetString(card.DescriptionResourceKey))));
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
