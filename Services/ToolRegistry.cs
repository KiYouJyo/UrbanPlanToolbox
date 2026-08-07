using System.Collections.ObjectModel;
using UrbanPlanToolbox.Models.Tools;

namespace UrbanPlanToolbox.Services;

public sealed class ToolRegistry
{
    private readonly IReadOnlyDictionary<string, ToolDefinition> _toolsById;

    public static ToolRegistry Default { get; } = new(
    [
        new(
            ToolIds.PlanningIndicatorCalculator,
            "Tool_PlanningIndicator_Name",
            "Tool_PlanningIndicator_Description",
            ToolPrimaryCategory.Design,
            ToolSecondaryCategory.MasterPlanning,
            "\uE8EF",
            typeof(Views.PlanningCalculatorPage),
            10,
            true,
            "guihuazhibiaokuaisujisuanqi",
            "G",
            "Tool_PlanningIndicator_Keywords"),
        new(
            ToolIds.UnitScaleConverter,
            "Tool_UnitScaleConverter_Name",
            "Tool_UnitScaleConverter_Description",
            ToolPrimaryCategory.Design,
            ToolSecondaryCategory.DetailedDesign,
            "\uE8AB",
            typeof(Views.UnitScaleConverterPage),
            20,
            true,
            "danweiyubilichihuansuanqi",
            "D",
            "Tool_UnitScaleConverter_Keywords"),
        new(
            ToolIds.ColorPaletteRecorder,
            "Tool_ColorPaletteRecorder_Name",
            "Tool_ColorPaletteRecorder_Description",
            ToolPrimaryCategory.Design,
            ToolSecondaryCategory.DetailedDesign,
            "\uE790",
            typeof(Views.ColorPaletteRecorderPage),
            30,
            true,
            "sekafanganjiluyiqi",
            "S",
            "Tool_ColorPaletteRecorder_Keywords")
        {
            CategoryPlacements = [
                new(ToolPrimaryCategory.Design, ToolSecondaryCategory.DetailedDesign, 30),
                new(ToolPrimaryCategory.Research, ToolSecondaryCategory.ResearchPreparation, 30)]
        },
        new(
            ToolIds.WorkflowReviewChecklist,
            "Tool_WorkflowReviewChecklist_Name",
            "Tool_WorkflowReviewChecklist_Description",
            ToolPrimaryCategory.Design,
            ToolSecondaryCategory.PreliminaryAnalysis,
            "\uE8FD",
            typeof(Views.WorkflowReviewChecklistPage),
            40,
            true,
            "liuchengshenheqingdan",
            "L",
            "Tool_WorkflowReviewChecklist_Keywords")
        {
            CategoryPlacements = [
                new(ToolPrimaryCategory.Design, ToolSecondaryCategory.PreliminaryAnalysis, 40),
                new(ToolPrimaryCategory.Research, ToolSecondaryCategory.ResearchPreparation, 40)]
        },
        new(
            ToolIds.RegulationsIndex,
            "Tool_RegulationsIndex_Name",
            "Tool_RegulationsIndex_Description",
            ToolPrimaryCategory.Design,
            ToolSecondaryCategory.MasterPlanning,
            "\uE71D",
            typeof(Views.RegulationsIndexPage),
            50,
            true,
            "xingyefaguizhishiku",
            "X",
            "Tool_RegulationsIndex_Keywords")
        {
            CategoryPlacements = [
                new(ToolPrimaryCategory.Design, ToolSecondaryCategory.MasterPlanning, 50),
                new(ToolPrimaryCategory.Research, ToolSecondaryCategory.ResearchPreparation, 50)]
        },
        new(
            ToolIds.DesignConceptDictionary,
            "Tool_DesignConceptDictionary_Name",
            "Tool_DesignConceptDictionary_Description",
            ToolPrimaryCategory.Design,
            ToolSecondaryCategory.DesignDevelopment,
            "\uE8A4",
            typeof(Views.DesignConceptDictionaryPage),
            60,
            true,
            "shejilinian cidian",
            "D",
            "Tool_DesignConceptDictionary_Keywords")
        {
            CategoryPlacements = [new(ToolPrimaryCategory.Design, ToolSecondaryCategory.DesignDevelopment, 60)]
        },
        new(
            ToolIds.CoordinateSystemConverter,
            "Tool_CoordinateSystemConverter_Name",
            "Tool_CoordinateSystemConverter_Description",
            ToolPrimaryCategory.Research,
            ToolSecondaryCategory.GeographicTools,
            "\uE81C",
            typeof(Views.CoordinateSystemConverterPage),
            70,
            true,
            "zuobiaoxizhuanhuanqi",
            "Z",
            "Tool_CoordinateSystemConverter_Keywords"),
        new(
            ToolIds.PlanningTerminology,
            "Tool_PlanningTerminology_Name",
            "Tool_PlanningTerminology_Description",
            ToolPrimaryCategory.Design,
            ToolSecondaryCategory.PreliminaryAnalysis,
            "\uE8A4",
            typeof(Views.PlanningTerminologyPage),
            80,
            true,
            "zhongriyingguihuashuyuku",
            "P",
            "Tool_PlanningTerminology_Keywords")
        {
            CategoryPlacements = [
                new(ToolPrimaryCategory.Design, ToolSecondaryCategory.PreliminaryAnalysis, 80),
                new(ToolPrimaryCategory.Research, ToolSecondaryCategory.ResearchPreparation, 15)]
        }
    ]);

    public ToolRegistry(IEnumerable<ToolDefinition> tools)
    {
        ArgumentNullException.ThrowIfNull(tools);

        var ordered = tools.OrderBy(tool => tool.SortOrder).ThenBy(tool => tool.Id, StringComparer.Ordinal).ToArray();
        if (ordered.Any(tool => string.IsNullOrWhiteSpace(tool.Id)))
        {
            throw new ArgumentException("Tool IDs cannot be empty.", nameof(tools));
        }

        if (ordered.Any(tool => string.IsNullOrWhiteSpace(tool.PinyinSortKey) ||
                                string.IsNullOrWhiteSpace(tool.PinyinInitial) ||
                                (tool.Searchable && string.IsNullOrWhiteSpace(tool.SearchKeywordsResourceKey)) ||
                                string.IsNullOrWhiteSpace(tool.NameResourceKey) ||
                                string.IsNullOrWhiteSpace(tool.DescriptionResourceKey)))
        {
            throw new ArgumentException("Tool search metadata must be complete.", nameof(tools));
        }

        if (ordered.Any(tool => !Enum.IsDefined(tool.PrimaryCategory) ||
                                !Enum.IsDefined(tool.SecondaryCategory) ||
                                !Enum.IsDefined(tool.Visibility) ||
                                !tool.PageType.IsClass || tool.PageType.IsAbstract))
        {
            throw new ArgumentException("Tool metadata contains an invalid category, visibility, or page type.", nameof(tools));
        }

        var duplicateId = ordered
            .GroupBy(tool => tool.Id, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicateId is not null)
        {
            throw new ArgumentException($"Duplicate tool ID: {duplicateId}", nameof(tools));
        }

        All = Array.AsReadOnly(ordered);
        _toolsById = new ReadOnlyDictionary<string, ToolDefinition>(
            ordered.ToDictionary(tool => tool.Id, StringComparer.Ordinal));
    }

    public IReadOnlyList<ToolDefinition> All { get; }

    public ToolDefinition GetById(string id) =>
        _toolsById.TryGetValue(id, out var tool)
            ? tool
            : throw new KeyNotFoundException($"No tool is registered with ID '{id}'.");

    public bool TryGet(string? id, out ToolDefinition? tool)
    {
        if (id is null)
        {
            tool = null;
            return false;
        }

        return _toolsById.TryGetValue(id, out tool);
    }

    public IReadOnlyList<ToolDefinition> GetByPrimaryCategory(ToolPrimaryCategory category) =>
        All.Where(tool => tool.GetPlacements().Any(placement => placement.PrimaryCategory == category)).ToArray();

    public IReadOnlyList<ToolDefinition> GetAvailableByPrimaryCategory(ToolPrimaryCategory category) =>
        All.Where(tool => IsVisible(tool) && tool.GetPlacements().Any(placement => placement.PrimaryCategory == category)).ToArray();

    public IReadOnlyList<ToolDefinition> GetBySecondaryCategory(ToolSecondaryCategory category) =>
        All.Where(tool => tool.GetPlacements().Any(placement => placement.SecondaryCategory == category)).ToArray();

    public IReadOnlyList<ToolDefinition> GetAvailableByCategories(
        ToolPrimaryCategory primaryCategory,
        ToolSecondaryCategory secondaryCategory) =>
        All.Where(tool => IsVisible(tool) && tool.GetPlacements().Any(placement =>
                placement.PrimaryCategory == primaryCategory &&
                placement.SecondaryCategory == secondaryCategory))
            .ToArray();

    public IReadOnlyList<ToolDefinition> GetVisibleTools() =>
        All.Where(IsVisible).ToArray();

    private static bool IsVisible(ToolDefinition tool) =>
        tool.IsAvailable && tool.Visibility == ToolVisibility.Visible;
}
