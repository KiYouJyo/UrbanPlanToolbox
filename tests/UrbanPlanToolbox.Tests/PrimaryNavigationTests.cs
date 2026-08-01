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
            PrimaryNavigationIds.DesignTools,
            PrimaryNavigationIds.ResearchTools,
            PrimaryNavigationIds.ProjectArchive,
            PrimaryNavigationIds.About
        ], ids);
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal("Navigation_Search", PrimaryNavigation.Default.All.Single(route => route.Id == PrimaryNavigationIds.CommonTools).NameResourceKey);
    }

    [Theory]
    [InlineData(PrimaryNavigationIds.Welcome, typeof(Views.HomePage))]
    [InlineData(PrimaryNavigationIds.CommonTools, typeof(Views.CommonToolsPage))]
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
