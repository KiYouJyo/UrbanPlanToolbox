using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class FirstRunGuideContractTests
{
    [Fact]
    public void GuideIsAWindowLevelOpaqueSingleSurfaceWithFixedBodyAndFooter()
    {
        var root = FindRepositoryRoot();
        var window = File.ReadAllText(Path.Combine(root, "MainWindow.xaml"));
        var guide = File.ReadAllText(Path.Combine(root, "Views", "FirstRunGuideHost.xaml"));

        Assert.Contains("x:Name=\"FirstRunGuide\"", window);
        Assert.Contains("Grid.RowSpan=\"2\"", window);
        Assert.Contains("Canvas.ZIndex=\"10\"", window);
        Assert.Contains("Background=\"{ThemeResource SolidBackgroundFillColorBaseBrush}\"", guide);
        Assert.Contains("x:Name=\"OverlayRoot\"", guide);
        Assert.Contains("x:Name=\"GuideCard\"", guide);
        Assert.Contains("x:Name=\"BodyScrollViewer\"", guide);
        Assert.Contains("Grid.Row=\"2\"", guide);
        Assert.Contains("GettingFocus=\"OnGettingFocus\"", guide);
        Assert.DoesNotContain("SettingsPage", guide);
    }

    [Fact]
    public void GuideCoordinatorGuardsAgainstDuplicateInstancesAndRestoresFocus()
    {
        var root = FindRepositoryRoot();
        var code = File.ReadAllText(Path.Combine(root, "MainWindow.xaml.cs"));

        Assert.Contains("_firstRunGuideShowing", code);
        Assert.Contains("FocusManager.GetFocusedElement", code);
        Assert.Contains("FirstRunGuideLaunchMode.Manual", code);
        Assert.Contains("FirstRunGuide.Show(mode)", code);
        Assert.Contains("_focusBeforeFirstRunGuide", code);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "UrbanPlanToolbox.slnx"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}
