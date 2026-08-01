namespace UrbanPlanToolbox.Models.Tools;

public sealed record ToolDefinition(
    string Id,
    string DisplayName,
    string Description,
    ToolPrimaryCategory PrimaryCategory,
    ToolSecondaryCategory SecondaryCategory,
    string IconGlyph,
    Type PageType,
    int SortOrder,
    bool IsAvailable,
    string PinyinSortKey,
    string PinyinInitial,
    IReadOnlyList<string> SearchKeywords);
