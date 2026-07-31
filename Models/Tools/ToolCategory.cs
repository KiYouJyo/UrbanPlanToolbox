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
