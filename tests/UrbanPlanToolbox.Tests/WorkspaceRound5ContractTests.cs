using System.Xml.Linq;
using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class WorkspaceRound5ContractTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void TimelineTilesExposeMilestoneNotesAndScrollableContent()
    {
        var source = File.ReadAllText(Round5Path());
        Assert.Contains("milestone.Notes", source, StringComparison.Ordinal);
        Assert.Contains("VerticalScrollBarVisibility = ScrollBarVisibility.Auto", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ViewerReservesNavigationColumnsAndCentersFitContent()
    {
        var source = File.ReadAllText(Round5Path());
        Assert.Contains("media.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) })", source, StringComparison.Ordinal);
        Assert.Contains("Grid.SetColumn(viewport, 1)", source, StringComparison.Ordinal);
        Assert.Contains("HorizontalContentAlignment = HorizontalAlignment.Center", source, StringComparison.Ordinal);
        Assert.Contains("VerticalContentAlignment = VerticalAlignment.Center", source, StringComparison.Ordinal);
        Assert.Contains("Grid.SetColumn(zoomIn, 4)", source, StringComparison.Ordinal);
        Assert.Contains("Grid.SetColumn(fit, 5)", source, StringComparison.Ordinal);
        Assert.Contains("Grid.SetColumn(close, 6)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void WorkspaceRemovesOverviewPhaseBadgeAndPanelSettingsMenu()
    {
        var document = XDocument.Load(WorkspacePath());
        var phaseBadge = document.Descendants(Presentation + "Border")
            .Single(element => (string?)element.Attribute(Xaml + "Name") == "OverviewPhaseBadge");
        Assert.Equal("Collapsed", (string?)phaseBadge.Attribute("Visibility"));

        var source = File.ReadAllText(Round5Path());
        Assert.DoesNotContain("Panel settings", source, StringComparison.Ordinal);
        Assert.DoesNotContain("面板设置", source, StringComparison.Ordinal);
        Assert.Contains("CreateRound5PanelMenu", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ResetPreservesPanelSettingsInsteadOfReplacingPanelData()
    {
        var source = File.ReadAllText(ResetServicePath());
        Assert.Contains("CreateDefaultPreservingPanelData", source, StringComparison.Ordinal);
        Assert.Contains("new Dictionary<string, string>(existing.Settings", source, StringComparison.Ordinal);
        Assert.Contains("Id = existing.Id", source, StringComparison.Ordinal);

        var pageSource = File.ReadAllText(Round5Path());
        Assert.Contains("ProjectWorkspaceResetService.CreateDefaultPreservingPanelData", pageSource, StringComparison.Ordinal);
    }

    [Fact]
    public void FolderAccessFallsBackToDurableDisplayPathWhenAccessTokenExpires()
    {
        var source = File.ReadAllText(FolderAccessPath());
        Assert.Contains("StorageFolder.GetFolderFromPathAsync(reference.DisplayPath)", source, StringComparison.Ordinal);
        Assert.Contains("Package upgrades/reinstalls can invalidate FutureAccessList entries", source, StringComparison.Ordinal);
        Assert.DoesNotContain("reference.RequiresReselection || string.IsNullOrWhiteSpace(reference.AccessToken)", source, StringComparison.Ordinal);
    }

    private static string Round5Path() => FindFromRepository("Views", "ProjectWorkspacePage.Round5.cs");
    private static string WorkspacePath() => FindFromRepository("Views", "ProjectWorkspacePage.xaml");
    private static string ResetServicePath() => FindFromRepository("Services", "ProjectWorkspaceResetService.cs");
    private static string FolderAccessPath() => FindFromRepository("Services", "WindowsProjectFolderAccessService.cs");

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
