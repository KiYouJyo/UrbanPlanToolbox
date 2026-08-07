using UrbanPlanToolbox.Models.Tools;

namespace UrbanPlanToolbox.Services;

public sealed class ToolSearchService
{
    private readonly ToolRegistry _toolRegistry;
    private readonly ILocalizationService _localization;

    public ToolSearchService(ToolRegistry toolRegistry, ILocalizationService localization)
    {
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
    }

    public IReadOnlyList<ToolSearchGroup> Search(string? query, Func<ToolDefinition, bool> isFavorite)
    {
        ArgumentNullException.ThrowIfNull(isFavorite);

        var normalizedQuery = query?.Trim() ?? string.Empty;
        var matchingTools = _toolRegistry.All
            .Where(tool => tool.IsAvailable && tool.Visibility == ToolVisibility.Visible && tool.Searchable && Matches(tool, normalizedQuery, _localization))
            .OrderByDescending(isFavorite)
            .ThenBy(tool => tool.PinyinSortKey, StringComparer.Ordinal)
            .ThenBy(tool => tool.Id, StringComparer.Ordinal)
            .ToArray();

        var groups = new List<ToolSearchGroup>();
        var favorites = matchingTools.Where(isFavorite).ToArray();
        if (favorites.Length > 0)
        {
            groups.Add(new ToolSearchGroup(_localization.GetString("Search_FavoritesHeader"), Resolve(favorites), true));
        }

        foreach (var group in matchingTools
            .Where(tool => !isFavorite(tool))
            .GroupBy(GetInitial)
            .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            groups.Add(new ToolSearchGroup(group.Key, Resolve(group.ToArray()), false));
        }

        return groups;
    }

    private IReadOnlyList<LocalizedTool> Resolve(IEnumerable<ToolDefinition> tools) =>
        tools.Select(tool => new LocalizedTool(
                tool,
                _localization.GetString(tool.NameResourceKey),
                _localization.GetString(tool.DescriptionResourceKey)))
            .ToArray();

    private bool Matches(ToolDefinition tool, string query, ILocalizationService localization) =>
        string.IsNullOrEmpty(query) ||
        Contains(localization.GetString(tool.NameResourceKey), query) ||
        Contains(localization.GetString(tool.DescriptionResourceKey), query) ||
        Contains(tool.Id, query) ||
        Contains(tool.PinyinSortKey, query) ||
        Contains(tool.PinyinInitial, query) ||
        ResolveKeywords(tool, localization).Any(keyword => Contains(keyword, query));

    private IEnumerable<string> ResolveKeywords(ToolDefinition tool, ILocalizationService localization) =>
        localization.GetString(tool.SearchKeywordsResourceKey)
            .Split(['\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static bool Contains(string value, string query) =>
        value.Contains(query, StringComparison.OrdinalIgnoreCase);

    private static string GetInitial(ToolDefinition tool)
    {
        var initial = tool.PinyinInitial.Trim().ToUpperInvariant();
        return initial.Length == 1 && initial[0] is >= 'A' and <= 'Z' ? initial : "#";
    }
}
