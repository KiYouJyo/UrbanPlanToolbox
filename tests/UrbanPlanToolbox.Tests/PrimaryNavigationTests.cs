using UrbanPlanToolbox.Models.Navigation;
using UrbanPlanToolbox.Services;
using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class PrimaryNavigationTests
{
    [Fact]
    public void DefaultRoutesHaveUniqueStableIdsInNavigationOrder()
    {
        var ids = PrimaryNavigation.Default.All.Select(route => route.Id).ToArray();

        Assert.Equal(
        [
            PrimaryNavigationIds.Welcome,
            PrimaryNavigationIds.CommonTools,
            PrimaryNavigationIds.Database,
            PrimaryNavigationIds.DesignTools,
            PrimaryNavigationIds.ResearchTools,
            PrimaryNavigationIds.ProjectArchive,
            PrimaryNavigationIds.About
        ], ids);
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal("Navigation_Projects", PrimaryNavigation.Default.All[0].NameResourceKey);
        Assert.Equal("Navigation_Search", PrimaryNavigation.Default.All.Single(route => route.Id == PrimaryNavigationIds.CommonTools).NameResourceKey);
        Assert.DoesNotContain(PrimaryNavigation.Default.All, route => route.Id is "favorites" or "common-tools-page");
    }

    [Theory]
    [InlineData(PrimaryNavigationIds.Welcome, typeof(Views.HomePage))]
    [InlineData(PrimaryNavigationIds.CommonTools, typeof(Views.CommonToolsPage))]
    [InlineData(PrimaryNavigationIds.Database, typeof(Views.DatabasePage))]
    [InlineData(PrimaryNavigationIds.DesignTools, typeof(Views.DesignToolsPage))]
    [InlineData(PrimaryNavigationIds.ResearchTools, typeof(Views.ResearchToolsPage))]
    [InlineData(PrimaryNavigationIds.ProjectArchive, typeof(Views.ProjectArchivePage))]
    [InlineData(PrimaryNavigationIds.About, typeof(Views.AboutPage))]
    public void ResolvesStableIdToExpectedPageType(string id, Type expectedPageType)
    {
        Assert.True(PrimaryNavigation.Default.TryGet(id, out var route));
        Assert.Equal(expectedPageType, route!.PageType);
    }

    [Fact]
    public void UnfinishedDatabaseEntryIsHiddenWithoutRemovingItsRoute()
    {
        var root = FindRepositoryRoot();
        var sidebar = File.ReadAllText(Path.Combine(root, "MainPage.xaml"));
        var code = File.ReadAllText(Path.Combine(root, "MainPage.xaml.cs"));
        Assert.DoesNotContain("DatabaseItem", sidebar);
        Assert.DoesNotContain("DatabaseItem", code);
        Assert.DoesNotContain("Tag=\"database\"", sidebar);
        Assert.True(PrimaryNavigation.Default.TryGet(PrimaryNavigationIds.Database, out var route));
        Assert.Equal(typeof(Views.DatabasePage), route!.PageType);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "UrbanPlanToolbox.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }

    [Fact]
    public void ExpandedNavigationPaneRevealsAcrylicInsteadOfOpaqueCustomColor()
    {
        var root = FindRepositoryRoot();
        var resources = File.ReadAllText(Path.Combine(root, "App.xaml"));
        var sidebar = File.ReadAllText(Path.Combine(root, "MainPage.xaml"));
        var sidebarCode = File.ReadAllText(Path.Combine(root, "MainPage.xaml.cs"));
        var mainWindow = File.ReadAllText(Path.Combine(root, "MainWindow.xaml"));

        // Left-expanded NavigationView uses its own brush, independent of Window.SystemBackdrop.
        const string transparentBrush =
            "<SolidColorBrush x:Key=\"NavigationViewExpandedPaneBackground\" Color=\"Transparent\" />";
        Assert.Equal(2, resources.Split(transparentBrush, StringSplitOptions.None).Length - 1);
        Assert.Contains(
            "<SolidColorBrush x:Key=\"NavigationViewExpandedPaneBackground\" Color=\"{ThemeResource SystemColorWindowColor}\" />",
            resources);
        Assert.DoesNotContain("ShellNavigationPaneBackgroundBrush", resources);
        Assert.DoesNotContain("ShellNavigationPaneInactiveBackgroundBrush", resources);

        Assert.Contains("<NavigationView x:Name=\"Navigation\" Background=\"Transparent\"", sidebar);
        Assert.DoesNotContain("PaneBackground =", sidebarCode);
        Assert.DoesNotContain("HookNavigationPaneBackground", sidebarCode);
        Assert.Contains("<DesktopAcrylicBackdrop", mainWindow);
        Assert.DoesNotContain("<MicaBackdrop", mainWindow);
    }

    [Fact]
    public void MissingRouteReturnsSafeFailure()
    {
        Assert.False(PrimaryNavigation.Default.TryGet("removed-route", out var route));
        Assert.Null(route);
        Assert.False(PrimaryNavigation.Default.TryGet(null, out route));
        Assert.Null(route);
    }

    [Fact]
    public void DuplicateRouteIdIsRejected()
    {
        var routes = new[]
        {
            new PrimaryNavigationDefinition("duplicate", "One", typeof(Views.HomePage)),
            new PrimaryNavigationDefinition("duplicate", "Two", typeof(Views.AboutPage))
        };

        Assert.Throws<ArgumentException>(() => new PrimaryNavigation(routes));
    }
}
