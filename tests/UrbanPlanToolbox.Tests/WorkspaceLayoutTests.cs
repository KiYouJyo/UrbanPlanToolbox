using System.Xml.Linq;
using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class WorkspaceLayoutTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void WorkspaceUsesStretchingScrollLayoutAndResponsiveCoordinateGrid()
    {
        var document = XDocument.Load(WorkspacePath());
        var scrollViewer = Assert.Single(document.Root!.Elements(Presentation + "ScrollViewer"));
        Assert.Equal("Stretch", (string?)scrollViewer.Attribute("HorizontalContentAlignment"));
        Assert.Equal("Disabled", (string?)scrollViewer.Attribute("HorizontalScrollMode"));

        var content = document.Descendants(Presentation + "Grid")
            .Single(element => (string?)element.Attribute(Xaml + "Name") == "WorkspaceContent");
        Assert.Equal("Left", (string?)content.Attribute("HorizontalAlignment"));
        Assert.Equal("1400", (string?)content.Attribute("MaxWidth"));
        Assert.Equal("{Binding ViewportWidth, ElementName=WorkspaceScrollViewer}", (string?)content.Attribute("Width"));

        var coordinates = document.Descendants(Presentation + "Grid")
            .Single(element => (string?)element.Attribute(Xaml + "Name") == "CoordinatesGrid");
        Assert.NotEmpty(coordinates.Descendants(Presentation + "AdaptiveTrigger"));
        Assert.Equal(2, coordinates.Descendants(Presentation + "ColumnDefinition").Count());

        var expanders = document.Descendants(Presentation + "Expander").ToArray();
        Assert.NotEmpty(expanders);
        Assert.All(expanders, expander =>
        {
            Assert.Equal("Stretch", (string?)expander.Attribute("HorizontalAlignment"));
            Assert.Equal("Stretch", (string?)expander.Attribute("HorizontalContentAlignment"));
        });
    }

    [Fact]
    public void WorkspaceContainsEditableFinalSectionsAndNoLegacyTodoSnapshotSections()
    {
        var document = XDocument.Load(WorkspacePath());
        var names = document.Descendants()
            .Select(element => (string?)element.Attribute(Xaml + "Name"))
            .Where(name => name is not null)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("BasicInfoExpander", names);
        Assert.Contains("DescriptionBox", names);
        Assert.Contains("PlanningRequirementsBox", names);
        Assert.Contains("ResearchDetailsExpander", names);
        Assert.Contains("ResearchFieldBox", names);
        Assert.Contains("ResearchSubjectBox", names);
        Assert.Contains("ResearchMethodsBox", names);
        Assert.Contains("MilestoneList", names);
        Assert.Contains("FolderExpander", names);
        Assert.Contains("ManagementExpander", names);
        Assert.Contains("DeleteButton", names);
        Assert.DoesNotContain("TodosExpander", names);
        Assert.DoesNotContain("SnapshotsExpander", names);
    }

    private static string WorkspacePath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "Views", "ProjectWorkspacePage.xaml");
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate ProjectWorkspacePage.xaml.");
    }
}
