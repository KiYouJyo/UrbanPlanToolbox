using UrbanPlanToolbox.Models.Projects;
using UrbanPlanToolbox.Services;
using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class ProjectPresentationTests
{
    [Theory]
    [InlineData(ProjectTypeCodes.Coursework, "Coursework localized")]
    [InlineData(ProjectTypeCodes.Competition, "Competition localized")]
    [InlineData(ProjectTypeCodes.Research, "Research localized")]
    [InlineData(ProjectTypeCodes.Professional, "Professional localized")]
    [InlineData(ProjectTypeCodes.Personal, "Personal localized")]
    [InlineData(ProjectTypeCodes.Other, "Other localized")]
    public void StableTypeCodesResolveThroughLocalization(string code, string expected)
    {
        var localization = new DictionaryLocalizationService(new Dictionary<string, string>
        {
            ["ProjectType_Coursework"] = "Coursework localized",
            ["ProjectType_Competition"] = "Competition localized",
            ["ProjectType_Research"] = "Research localized",
            ["ProjectType_Professional"] = "Professional localized",
            ["ProjectType_Personal"] = "Personal localized",
            ["ProjectType_Other"] = "Other localized"
        });
        Assert.Equal(expected, ProjectPresentation.GetTypeName(code, localization));
    }

    [Fact]
    public void CustomOtherTypeOverridesLocalizedOtherLabelWithoutChangingCode()
    {
        var now = DateTimeOffset.UtcNow;
        var project = new ProjectRecord { Id = Guid.NewGuid(), Name = "P", Type = ProjectTypeCodes.Other, CustomType = "Studio", CreatedAtUtc = now, UpdatedAtUtc = now };
        Assert.Equal("Studio", ProjectPresentation.GetTypeName(project, new DictionaryLocalizationService(new Dictionary<string, string>())));
        Assert.Equal(ProjectTypeCodes.Other, project.Type);
    }

    [Fact]
    public async Task FolderAccessContractKeepsTokenSeparateAndSupportsExpiredAccess()
    {
        IProjectFolderAccessService access = new FakeFolderAccess();
        var selected = await access.SelectAsync(Guid.NewGuid());
        Assert.True(selected.Succeeded);
        Assert.Equal("local-token", selected.Reference!.AccessToken);
        Assert.Equal("C:\\Visible", selected.Reference.DisplayPath);
        var expired = await access.OpenAsync(new ProjectFolderReference { DisplayName = "Visible", DisplayPath = "C:\\Visible", RequiresReselection = true });
        Assert.False(expired.Succeeded);
        Assert.Equal("ProjectFolder_RequiresReselection", expired.ErrorKey);
    }

    private sealed class FakeFolderAccess : IProjectFolderAccessService
    {
        public Task<ProjectFolderAccessResult> SelectAsync(Guid projectId, ProjectFolderReference? current = null) => Task.FromResult(new ProjectFolderAccessResult(true, new() { AccessToken = "local-token", DisplayName = "Visible", DisplayPath = "C:\\Visible" }));
        public Task<ProjectFolderAccessResult> OpenAsync(ProjectFolderReference reference) => Task.FromResult(reference.RequiresReselection ? new ProjectFolderAccessResult(false, ErrorKey: "ProjectFolder_RequiresReselection") : new ProjectFolderAccessResult(true, reference));
        public void Clear(ProjectFolderReference? reference) { }
    }
}
