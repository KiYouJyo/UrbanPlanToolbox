using UrbanPlanToolbox.Models.Projects;
using UrbanPlanToolbox.Services;
using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class ProjectCreationDialogContractTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void NewProjectDialogs_OnlyCollectProjectIdentity()
    {
        var homePage = Read("Views/HomePage.xaml.cs");
        var start = homePage.IndexOf("private async Task ShowCreateDialogAsync", StringComparison.Ordinal);
        var end = homePage.IndexOf("private TextBox CreateTextBox", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var method = homePage[start..end];

        Assert.Contains("Project_Field_Name", method);
        Assert.Contains("Project_Field_Type", method);
        Assert.Contains("ResearchProject_Field_Type", method);
        Assert.Contains("Project_Field_CustomType", method);

        Assert.DoesNotContain("Project_Field_AdministrativeArea", method);
        Assert.DoesNotContain("Project_Field_Latitude", method);
        Assert.DoesNotContain("Project_Field_Longitude", method);
        Assert.DoesNotContain("Project_Field_Description", method);
        Assert.DoesNotContain("Project_Field_PlanningRequirements", method);
        Assert.DoesNotContain("ResearchProject_Field_Field", method);
        Assert.DoesNotContain("ResearchProject_Field_Subject", method);
        Assert.DoesNotContain("ResearchProject_Field_Methods", method);
        Assert.DoesNotContain("new ScrollViewer", method);

        Assert.Contains("CreateAsync(name.Text, selected.Code, customType.Text)", method);
        Assert.Contains("CreateResearchAsync(name.Text, selected.Code, customType.Text, null, null, null)", method);
    }

    [Fact]
    public void ResearchDetails_CanBeCompletedAfterProjectCreation()
    {
        var now = DateTimeOffset.UtcNow;
        var project = new ProjectRecord
        {
            Id = Guid.NewGuid(),
            Kind = ProjectKindCodes.Research,
            Name = "Research shell",
            Type = ResearchProjectTypeCodes.Thesis,
            ResearchDetails = new(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        var errors = ProjectValidation.Validate(project);

        Assert.DoesNotContain("ResearchFieldRequired", errors);
        Assert.DoesNotContain("ResearchSubjectRequired", errors);
        Assert.DoesNotContain("ResearchMethodsRequired", errors);
        Assert.Empty(errors);
    }

    private static string Read(string relativePath) => File.ReadAllText(Path.Combine(RepositoryRoot, relativePath));

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "UrbanPlanToolbox.csproj")) &&
                File.Exists(Path.Combine(directory.FullName, "Views", "HomePage.xaml.cs")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("UrbanPlanToolbox repository root with source files was not found from the test output directory.");
    }
}
