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

    [Fact]
    public void AboutUpdateCardKeepsMetadataRegionsVisibleWhenTargetVersionIsUnknown()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "Views", "AboutPage.xaml.cs"));

        Assert.Contains("UpdateTargetLabel.Visibility = Visibility.Visible;", source);
        Assert.Contains("UpdateTargetText.Visibility = Visibility.Visible;", source);
        Assert.Contains("UpdateNotesLabel.Visibility = Visibility.Visible;", source);
        Assert.Contains("UpdateNotesContainer.Visibility = Visibility.Visible;", source);
        Assert.Contains("UpdateNotesText.Visibility = Visibility.Visible;", source);
        Assert.Contains("UpdateTargetText.Text = hasTrustedTargetVersion ? $\"v{info.AvailableVersion}\" : \"—\";", source);
        Assert.Contains("UpdateNotesText.Text = hasTrustedTargetVersion ? display.Text : T(\"Update_ReleaseNotesUnavailable\");", source);
        Assert.DoesNotContain("UpdateTargetLabel.Visibility = Visibility.Collapsed", source);
        Assert.DoesNotContain("UpdateTargetText.Visibility = Visibility.Collapsed", source);
        Assert.DoesNotContain("UpdateNotesLabel.Visibility = Visibility.Collapsed", source);
        Assert.DoesNotContain("UpdateNotesContainer.Visibility = Visibility.Collapsed", source);
        Assert.DoesNotContain("UpdateNotesText.Visibility = Visibility.Collapsed", source);
    }

    [Fact]
    public void AboutUpdateCardUsesResponsiveFigmaV2LayoutAndOnlyButtonSpinnerForChecking()
    {
        var root = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "Views", "AboutPage.xaml"));
        var code = File.ReadAllText(Path.Combine(root, "Views", "AboutPage.xaml.cs"));

        Assert.Contains("MaxWidth=\"840\"", xaml);
        Assert.Contains("x:Name=\"UpdateBodyGrid\"", xaml);
        Assert.Contains("x:Name=\"CompactUpdateLayout\"", xaml);
        Assert.Contains("x:Name=\"WideUpdateLayout\"", xaml);
        Assert.Contains("AdaptiveTrigger MinWindowWidth=\"980\"", xaml);
        Assert.Contains("UpdateMetadataCard.(Grid.Row)", xaml);
        Assert.Contains("UpdateMetadataCard.(Grid.ColumnSpan)", xaml);
        Assert.Contains("x:Name=\"CheckUpdateButtonProgressRing\"", xaml);
        Assert.DoesNotContain("x:Name=\"UpdateStatusProgressRing\"", xaml);
        Assert.Contains("CheckUpdateButtonProgressRing.Visibility = checking ? Visibility.Visible : Visibility.Collapsed;", code);
        Assert.Contains("CheckUpdateButtonProgressRing.IsActive = checking;", code);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "UrbanPlanToolbox.slnx"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}
