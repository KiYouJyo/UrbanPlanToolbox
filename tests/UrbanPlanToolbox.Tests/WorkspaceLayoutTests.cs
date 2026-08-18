using System.Xml.Linq;
using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class WorkspaceLayoutTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void WorkspaceUsesFixedOverviewAndCustomTileSurface()
    {
        var document = XDocument.Load(WorkspacePath());
        var scrollViewer = Assert.Single(document.Root!.Elements(Presentation + "Grid")
            .Elements(Presentation + "ScrollViewer"));
        Assert.Equal("Stretch", (string?)scrollViewer.Attribute("HorizontalContentAlignment"));
        Assert.Equal("Disabled", (string?)scrollViewer.Attribute("HorizontalScrollMode"));

        var names = document.Descendants()
            .Select(element => (string?)element.Attribute(Xaml + "Name"))
            .Where(name => name is not null)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("OverviewCard", names);
        Assert.Contains("WorkspaceSurface", names);
        Assert.Contains("TileCanvas", names);
        Assert.Contains("AddPanelButton", names);
        Assert.DoesNotContain("EditLayoutButton", names);
        Assert.Contains("UndoLayoutButton", names);
        Assert.Contains("ResetLayoutButton", names);
        Assert.Contains("DrawerLayer", names);
        Assert.Contains("DrawerPane", names);
        Assert.Contains("DrawerContent", names);
        Assert.Contains("EditOverviewButton", names);
    }

    [Fact]
    public void WorkspaceRemovesDenseAlwaysVisibleFormAndExpanderLayout()
    {
        var document = XDocument.Load(WorkspacePath());
        Assert.Empty(document.Descendants(Presentation + "Expander"));

        var names = document.Descendants()
            .Select(element => (string?)element.Attribute(Xaml + "Name"))
            .Where(name => name is not null)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("BasicInfoExpander", names);
        Assert.DoesNotContain("ResearchDetailsExpander", names);
        Assert.DoesNotContain("DescriptionBox", names);
        Assert.DoesNotContain("ResearchFieldBox", names);
        Assert.DoesNotContain("MilestoneList", names);
        Assert.DoesNotContain("FolderExpander", names);
        Assert.DoesNotContain("ManagementExpander", names);
    }

    [Fact]
    public void WorkspaceLayoutEngineKeepsCanonicalTwelveColumnContract()
    {
        var source = File.ReadAllText(LayoutServicePath());
        Assert.Contains("public const int Columns = 12", source, StringComparison.Ordinal);
        Assert.Contains("CreateDefault", source, StringComparison.Ordinal);
        Assert.Contains("ProjectWorkspacePanelKinds.ImageShowcase", source, StringComparison.Ordinal);
        Assert.Contains("ProjectWorkspacePanelKinds.ResearchFramework", source, StringComparison.Ordinal);
        Assert.Contains("MovePanel", source, StringComparison.Ordinal);
        Assert.Contains("ResizePanel", source, StringComparison.Ordinal);
        Assert.Contains("ResolveCollisions", source, StringComparison.Ordinal);
    }

    [Fact]
    public void WorkspaceCodeImplementsResponsiveReadOnlyReflowAndProjectScopedAutosave()
    {
        var source = File.ReadAllText(WorkspaceCodePath());
        Assert.Contains("width >= 1280 ? 12 : width >= 960 ? 8 : width >= 720 ? 6 : 1", source, StringComparison.Ordinal);
        Assert.Contains("BuildResponsivePositions", source, StringComparison.Ordinal);
        Assert.Contains("PersistProjectAsync", source, StringComparison.Ordinal);
        Assert.Contains("WorkspaceLayout", source, StringComparison.Ordinal);
        Assert.Contains("RememberLayoutForUndo", source, StringComparison.Ordinal);
        Assert.Contains("OpenAddPanelDrawer", source, StringComparison.Ordinal);
        Assert.Contains("OnTilePointerPressed", source, StringComparison.Ordinal);
        Assert.Contains("ShowImageViewerAsync", source, StringComparison.Ordinal);
        Assert.Contains("Stretch = Stretch.Uniform", source, StringComparison.Ordinal);
        Assert.Contains("AdditionalFolders", File.ReadAllText(ProjectModelsPath()), StringComparison.Ordinal);
        Assert.DoesNotContain("复制面板", source, StringComparison.Ordinal);
    }

    private static string WorkspacePath() => FindFromRepository("Views", "ProjectWorkspacePage.xaml");
    private static string WorkspaceCodePath() => FindFromRepository("Views", "ProjectWorkspacePage.xaml.cs");
    private static string LayoutServicePath() => FindFromRepository("Services", "ProjectWorkspaceLayoutService.cs");
    private static string ProjectModelsPath() => FindFromRepository("Models", "Projects", "ProjectModels.cs");

    private static string FindFromRepository(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine([directory.FullName, .. parts]);
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException($"Could not locate {string.Join('/', parts)}.");
    }
}
