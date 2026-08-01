using UrbanPlanToolbox.Models.Projects;

namespace UrbanPlanToolbox.Services;

public static class ProjectPresentation
{
    public static string GetTypeName(string type, ILocalizationService localization) =>
        localization.GetString(type switch
        {
            ProjectTypeCodes.Coursework => "ProjectType_Coursework",
            ProjectTypeCodes.Competition => "ProjectType_Competition",
            ProjectTypeCodes.Research => "ProjectType_Research",
            ProjectTypeCodes.Professional => "ProjectType_Professional",
            ProjectTypeCodes.Personal => "ProjectType_Personal",
            _ => "ProjectType_Other"
        });

    public static string GetTypeName(ProjectRecord project, ILocalizationService localization) =>
        project.Type == ProjectTypeCodes.Other && !string.IsNullOrWhiteSpace(project.CustomType)
            ? project.CustomType
            : GetTypeName(project.Type, localization);
}
