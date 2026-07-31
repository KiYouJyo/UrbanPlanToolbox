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
            "规划指标快速计算器",
            "快速计算常用城市规划指标。",
            ToolPrimaryCategory.Design,
            ToolSecondaryCategory.MasterPlanning,
            "\uE8EF",
            typeof(Views.PlanningCalculatorPage),
            10,
            true),
        new(
            ToolIds.UnitScaleConverter,
            "单位与比例尺换算器",
            "换算规划设计常用单位与图纸比例尺。",
            ToolPrimaryCategory.Design,
            ToolSecondaryCategory.DetailedDesign,
            "\uE8AB",
            typeof(Views.UnitScaleConverterPage),
            20,
            true)
    ]);

    public ToolRegistry(IEnumerable<ToolDefinition> tools)
    {
        ArgumentNullException.ThrowIfNull(tools);

        var ordered = tools.OrderBy(tool => tool.SortOrder).ThenBy(tool => tool.Id, StringComparer.Ordinal).ToArray();
        if (ordered.Any(tool => string.IsNullOrWhiteSpace(tool.Id)))
        {
            throw new ArgumentException("Tool IDs cannot be empty.", nameof(tools));
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
}
