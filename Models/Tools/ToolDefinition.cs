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
    string SearchKeywordsResourceKey);
