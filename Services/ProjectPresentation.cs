using UrbanPlanToolbox.Models.Projects;

namespace UrbanPlanToolbox.Services;

public static class ProjectPresentation
{
    public static string GetKindName(string kind, ILocalizationService localization) =>
        localization.GetString(kind == ProjectKindCodes.Research ? "ProjectKind_Research" : "ProjectKind_Design");

    public static string GetTypeName(string type, ILocalizationService localization) => GetDesignTypeName(type, localization);

    public static string GetDesignTypeName(string type, ILocalizationService localization) =>
        localization.GetString(type switch
        {
            ProjectTypeCodes.Coursework => "ProjectType_Coursework",
            ProjectTypeCodes.Competition => "ProjectType_Competition",
            ProjectTypeCodes.Research => "ProjectType_Research",
            ProjectTypeCodes.Professional => "ProjectType_Professional",
            ProjectTypeCodes.Personal => "ProjectType_Personal",
            _ => "ProjectType_Other"
        });

    public static string GetResearchTypeName(string type, ILocalizationService localization) =>
        localization.GetString(type switch
        {
            ResearchProjectTypeCodes.Coursework => "ResearchProjectType_Coursework",
            ResearchProjectTypeCodes.Thesis => "ResearchProjectType_Thesis",
            ResearchProjectTypeCodes.Paper => "ResearchProjectType_Paper",
            ResearchProjectTypeCodes.ResearchProject => "ResearchProjectType_ResearchProject",
            _ => "ResearchProjectType_Other"
        });

    public static string GetTypeName(ProjectRecord project, ILocalizationService localization) =>
        project.Type == ProjectTypeCodes.Other && !string.IsNullOrWhiteSpace(project.CustomType)
            ? project.CustomType
            : project.Kind == ProjectKindCodes.Research
                ? GetResearchTypeName(project.Type, localization)
                : GetDesignTypeName(project.Type, localization);

    public static string CreateResearchSubjectSummary(string? value, int maxLength = 120)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var normalized = string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= maxLength ? normalized : $"{normalized[..(maxLength - 1)]}…";
    }
}
