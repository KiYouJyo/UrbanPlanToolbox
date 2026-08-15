using UrbanPlanToolbox.Services;
using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class StoreUpdateMetadataTests
{
    [Fact]
    public void StaleHostedManifestAndInstalledPackageVersionDoNotBecomeTargetVersion()
    {
        var resolved = StoreUpdateVersionResolver.Resolve(
            "1.7.5",
            ["1.7.5.0"],
            "1.7.4");

        Assert.Null(resolved);
    }

    [Fact]
    public void HigherStorePackageVersionWinsWhenAvailable()
    {
        var resolved = StoreUpdateVersionResolver.Resolve(
            "1.7.5",
            ["1.7.5.0", "1.8.0.0"],
            "1.7.4");

        Assert.Equal("1.8.0", resolved);
    }

    [Fact]
    public void NewerHostedManifestCanSupplyTargetWhenPackageMetadataOnlyShowsInstalledVersion()
    {
        var resolved = StoreUpdateVersionResolver.Resolve(
            "1.7.5",
            ["1.7.5.0"],
            "1.8.0");

        Assert.Equal("1.8.0", resolved);
    }

    [Fact]
    public void HighestStrictlyNewerCandidateIsSelected()
    {
        var resolved = StoreUpdateVersionResolver.Resolve(
            "1.8.0",
            ["1.8.1.0", "1.9.0.0"],
            "1.8.2");

        Assert.Equal("1.9.0", resolved);
    }
}
