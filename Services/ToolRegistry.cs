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
                                string.IsNullOrWhiteSpace(tool.SearchKeywordsResourceKey) ||
                                string.IsNullOrWhiteSpace(tool.NameResourceKey) ||
                                string.IsNullOrWhiteSpace(tool.DescriptionResourceKey)))
        {
            throw new ArgumentException("Tool search metadata must be complete.", nameof(tools));
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
        All.Where(tool => tool.PrimaryCategory == category).ToArray();

    public IReadOnlyList<ToolDefinition> GetAvailableByPrimaryCategory(ToolPrimaryCategory category) =>
        All.Where(tool => tool.PrimaryCategory == category && tool.IsAvailable).ToArray();

    public IReadOnlyList<ToolDefinition> GetBySecondaryCategory(ToolSecondaryCategory category) =>
        All.Where(tool => tool.SecondaryCategory == category).ToArray();

    public IReadOnlyList<ToolDefinition> GetAvailableByCategories(
        ToolPrimaryCategory primaryCategory,
        ToolSecondaryCategory secondaryCategory) =>
        All.Where(tool =>
                tool.PrimaryCategory == primaryCategory &&
                tool.SecondaryCategory == secondaryCategory &&
                tool.IsAvailable)
            .ToArray();
}
