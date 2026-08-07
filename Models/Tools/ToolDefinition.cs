namespace UrbanPlanToolbox.Models.Tools;

public sealed record ToolDefinition(
    string Id,
    string NameResourceKey,
    string DescriptionResourceKey,
    ToolPrimaryCategory PrimaryCategory,
    ToolSecondaryCategory SecondaryCategory,
    string IconGlyph,
    Type PageType,
    int SortOrder,
    bool IsAvailable,
    string PinyinSortKey,
    string PinyinInitial,
    string SearchKeywordsResourceKey)
{
    // These flags belong to the registry metadata so consumers do not need
    // separate per-page lists when a tool is added or temporarily hidden.
    public bool SupportsFavorites { get; init; } = true;
    public bool Searchable { get; init; } = true;
    public ToolVisibility Visibility { get; init; } = ToolVisibility.Visible;
    public IReadOnlyList<ToolPlacement> CategoryPlacements { get; init; } = [];
}

public enum ToolVisibility
{
    Visible,
    Hidden
}

public sealed record ToolPlacement(
    ToolPrimaryCategory PrimaryCategory,
    ToolSecondaryCategory SecondaryCategory,
    int SortOrder);

public static class ToolDefinitionExtensions
{
    public static IReadOnlyList<ToolPlacement> GetPlacements(this ToolDefinition tool) =>
        tool.CategoryPlacements.Count > 0
            ? tool.CategoryPlacements
            : [new ToolPlacement(tool.PrimaryCategory, tool.SecondaryCategory, tool.SortOrder)];
}
