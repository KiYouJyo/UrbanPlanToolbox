namespace UrbanPlanToolbox.Models.Tools;

public enum ToolPrimaryCategory
{
    Design,
    Research
}

public enum ToolSecondaryCategory
{
    PreliminaryAnalysis,
    FieldResearch,
    DesignDevelopment,
    MasterPlanning,
    DetailedDesign,
    ResearchPreparation,
    GeographicTools,
    DataTools
}

public static class ToolCategoryNames
{
    public static string GetDisplayName(this ToolPrimaryCategory category) => category switch
    {
        ToolPrimaryCategory.Design => "设计工具",
        ToolPrimaryCategory.Research => "科研工具",
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
    };

    public static string GetDisplayName(this ToolSecondaryCategory category) => category switch
    {
        ToolSecondaryCategory.PreliminaryAnalysis => "前期分析",
        ToolSecondaryCategory.FieldResearch => "实地调研",
        ToolSecondaryCategory.DesignDevelopment => "方案推导",
        ToolSecondaryCategory.MasterPlanning => "总体设计",
        ToolSecondaryCategory.DetailedDesign => "详细设计",
        ToolSecondaryCategory.ResearchPreparation => "前期工具",
        ToolSecondaryCategory.GeographicTools => "地理工具",
        ToolSecondaryCategory.DataTools => "数据工具",
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
    };
}

public sealed record ToolCategoryDefinition(
    string Id,
    string DisplayName,
    ToolPrimaryCategory PrimaryCategory,
    ToolSecondaryCategory SecondaryCategory,
    int SortOrder);

public static class ToolCategoryCatalog
{
    public static IReadOnlyList<ToolCategoryDefinition> Design { get; } = Array.AsReadOnly<ToolCategoryDefinition>(
    [
        new("preliminary-analysis", "前期分析", ToolPrimaryCategory.Design, ToolSecondaryCategory.PreliminaryAnalysis, 10),
        new("field-research", "实地调研", ToolPrimaryCategory.Design, ToolSecondaryCategory.FieldResearch, 20),
        new("design-development", "方案推导", ToolPrimaryCategory.Design, ToolSecondaryCategory.DesignDevelopment, 30),
        new("master-planning", "总体设计", ToolPrimaryCategory.Design, ToolSecondaryCategory.MasterPlanning, 40),
        new("detailed-design", "详细设计", ToolPrimaryCategory.Design, ToolSecondaryCategory.DetailedDesign, 50)
    ]);

    public static IReadOnlyList<ToolCategoryDefinition> Research { get; } = Array.AsReadOnly<ToolCategoryDefinition>(
    [
        new("research-preparation", "前期工具", ToolPrimaryCategory.Research, ToolSecondaryCategory.ResearchPreparation, 10),
        new("geographic-tools", "地理工具", ToolPrimaryCategory.Research, ToolSecondaryCategory.GeographicTools, 20),
        new("data-tools", "数据工具", ToolPrimaryCategory.Research, ToolSecondaryCategory.DataTools, 30)
    ]);

    public static IReadOnlyList<ToolCategoryDefinition> GetByPrimaryCategory(ToolPrimaryCategory category) => category switch
    {
        ToolPrimaryCategory.Design => Design,
        ToolPrimaryCategory.Research => Research,
        _ => []
    };

    public static bool TryGet(string? id, out ToolCategoryDefinition? category)
    {
        category = Design.Concat(Research).FirstOrDefault(
            item => string.Equals(item.Id, id, StringComparison.Ordinal));
        return category is not null;
    }
}
