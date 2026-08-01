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
    public static string GetNameResourceKey(this ToolPrimaryCategory category) => category switch
    {
        ToolPrimaryCategory.Design => "Navigation_DesignTools",
        ToolPrimaryCategory.Research => "Navigation_ResearchTools",
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
    };

    public static string GetNameResourceKey(this ToolSecondaryCategory category) => category switch
    {
        ToolSecondaryCategory.PreliminaryAnalysis => "Category_PreliminaryAnalysis",
        ToolSecondaryCategory.FieldResearch => "Category_FieldResearch",
        ToolSecondaryCategory.DesignDevelopment => "Category_DesignDevelopment",
        ToolSecondaryCategory.MasterPlanning => "Category_MasterPlanning",
        ToolSecondaryCategory.DetailedDesign => "Category_DetailedDesign",
        ToolSecondaryCategory.ResearchPreparation => "Category_ResearchPreparation",
        ToolSecondaryCategory.GeographicTools => "Category_GeographicTools",
        ToolSecondaryCategory.DataTools => "Category_DataTools",
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
    };
}

public sealed record ToolCategoryDefinition(
    string Id,
    string NameResourceKey,
    ToolPrimaryCategory PrimaryCategory,
    ToolSecondaryCategory SecondaryCategory,
    int SortOrder);

public static class ToolCategoryCatalog
{
    public static IReadOnlyList<ToolCategoryDefinition> Design { get; } = Array.AsReadOnly<ToolCategoryDefinition>(
    [
        new("preliminary-analysis", "Category_PreliminaryAnalysis", ToolPrimaryCategory.Design, ToolSecondaryCategory.PreliminaryAnalysis, 10),
        new("field-research", "Category_FieldResearch", ToolPrimaryCategory.Design, ToolSecondaryCategory.FieldResearch, 20),
        new("design-development", "Category_DesignDevelopment", ToolPrimaryCategory.Design, ToolSecondaryCategory.DesignDevelopment, 30),
        new("master-planning", "Category_MasterPlanning", ToolPrimaryCategory.Design, ToolSecondaryCategory.MasterPlanning, 40),
        new("detailed-design", "Category_DetailedDesign", ToolPrimaryCategory.Design, ToolSecondaryCategory.DetailedDesign, 50)
    ]);

    public static IReadOnlyList<ToolCategoryDefinition> Research { get; } = Array.AsReadOnly<ToolCategoryDefinition>(
    [
        new("research-preparation", "Category_ResearchPreparation", ToolPrimaryCategory.Research, ToolSecondaryCategory.ResearchPreparation, 10),
        new("geographic-tools", "Category_GeographicTools", ToolPrimaryCategory.Research, ToolSecondaryCategory.GeographicTools, 20),
        new("data-tools", "Category_DataTools", ToolPrimaryCategory.Research, ToolSecondaryCategory.DataTools, 30)
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
