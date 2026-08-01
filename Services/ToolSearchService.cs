using UrbanPlanToolbox.Models.Tools;

namespace UrbanPlanToolbox.Services;

public sealed class ToolSearchService
{
    private readonly ToolRegistry _toolRegistry;

    public ToolSearchService(ToolRegistry toolRegistry)
    {
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
    }

    public IReadOnlyList<ToolSearchGroup> Search(string? query, Func<ToolDefinition, bool> isFavorite)
    {
        ArgumentNullException.ThrowIfNull(isFavorite);

        var normalizedQuery = query?.Trim() ?? string.Empty;
        var matchingTools = _toolRegistry.All
            .Where(tool => tool.IsAvailable && Matches(tool, normalizedQuery))
            .OrderByDescending(isFavorite)
            .ThenBy(tool => tool.PinyinSortKey, StringComparer.Ordinal)
            .ThenBy(tool => tool.Id, StringComparer.Ordinal)
            .ToArray();

        var groups = new List<ToolSearchGroup>();
        var favorites = matchingTools.Where(isFavorite).ToArray();
        if (favorites.Length > 0)
        {
            groups.Add(new ToolSearchGroup("已收藏", favorites, true));
        }

        foreach (var group in matchingTools
            .Where(tool => !isFavorite(tool))
            .GroupBy(GetInitial)
            .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            groups.Add(new ToolSearchGroup(group.Key, group.ToArray(), false));
        }

        return groups;
    }

    private static bool Matches(ToolDefinition tool, string query) =>
        string.IsNullOrEmpty(query) ||
        Contains(tool.DisplayName, query) ||
        Contains(tool.Description, query) ||
        Contains(tool.Id, query) ||
        Contains(tool.PinyinSortKey, query) ||
        Contains(tool.PinyinInitial, query) ||
        tool.SearchKeywords.Any(keyword => Contains(keyword, query));

    private static bool Contains(string value, string query) =>
        value.Contains(query, StringComparison.OrdinalIgnoreCase);

    private static string GetInitial(ToolDefinition tool)
    {
        var initial = tool.PinyinInitial.Trim().ToUpperInvariant();
        return initial.Length == 1 && initial[0] is >= 'A' and <= 'Z' ? initial : "#";
    }
}
