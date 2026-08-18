using System.Text.Json.Nodes;
using UrbanPlanToolbox.Services;
using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class ProjectV4MigrationTests
{
    [Fact]
    public void ProjectContractIsVersionFour()
    {
        Assert.Equal(4, DataContractVersions.Project);
        Assert.Equal(4, ProjectStorageService.ProjectSchemaVersion);
    }

    [Fact]
    public void V3ProjectMigrationAddsWorkspaceContractWithoutInventingLayout()
    {
        var project = JsonNode.Parse("""
        {
          "id": "11111111-1111-1111-1111-111111111111",
          "kind": "design",
          "name": "Legacy project",
          "type": "coursework"
        }
        """)!;

        var migrated = new ProjectV3ToV4Migration().Apply(project);
        var root = Assert.IsType<JsonObject>(migrated);

        Assert.True(root.ContainsKey("workspaceLayout"));
        Assert.Null(root["workspaceLayout"]);
        Assert.Equal("Legacy project", root["name"]!.GetValue<string>());
    }

    [Fact]
    public void V3IndexMigrationDoesNotAddProjectOnlyWorkspaceField()
    {
        var index = JsonNode.Parse("""
        {
          "projects": [
            { "id": "11111111-1111-1111-1111-111111111111", "kind": "design", "name": "A", "type": "coursework" }
          ]
        }
        """)!;

        var migrated = new ProjectV3ToV4Migration().Apply(index);
        var root = Assert.IsType<JsonObject>(migrated);

        Assert.False(root.ContainsKey("workspaceLayout"));
        Assert.NotNull(root["projects"]);
    }
}
