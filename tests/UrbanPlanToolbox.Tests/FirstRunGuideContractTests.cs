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
    public void FirstRunOverlayIsOpaqueButExpandedNavigationRemainsAcrylicBacked()
    {
        var root = FindRepositoryRoot();
        var app = File.ReadAllText(Path.Combine(root, "App.xaml"));
        var guide = File.ReadAllText(Path.Combine(root, "Views", "FirstRunGuideHost.xaml"));
        var page = File.ReadAllText(Path.Combine(root, "MainPage.xaml"));
        var pageCode = File.ReadAllText(Path.Combine(root, "MainPage.xaml.cs"));

        Assert.Contains("Background=\"{ThemeResource SolidBackgroundFillColorBaseBrush}\"", guide);
        Assert.Contains("<ResourceDictionary x:Key=\"Light\">", app);
        Assert.Contains("<ResourceDictionary x:Key=\"Dark\">", app);
        Assert.Contains("<ResourceDictionary x:Key=\"HighContrast\">", app);
        Assert.Contains("NavigationViewExpandedPaneBackground\" Color=\"Transparent\"", app);
        Assert.Contains("NavigationViewExpandedPaneBackground\" Color=\"{ThemeResource SystemColorWindowColor}\"", app);
        Assert.Contains("<NavigationView x:Name=\"Navigation\" Background=\"Transparent\"", page);
        Assert.DoesNotContain("ShellNavigationPaneBackgroundBrush", guide + app + pageCode);
        Assert.DoesNotContain("FirstRunGuideBackgroundBrush", app);
        Assert.DoesNotContain("PaneBackground =", pageCode);
    }

    [Fact]
    public void FirstRunCardUsesApplicationCardStyleWithoutIndependentColorOverrides()
    {
        var root = FindRepositoryRoot();
        var guide = File.ReadAllText(Path.Combine(root, "Views", "FirstRunGuideHost.xaml"));
        var app = File.ReadAllText(Path.Combine(root, "App.xaml"));
        Assert.Contains("Style=\"{StaticResource SettingsSectionCardStyle}\"", guide);
        Assert.Contains("CardBackgroundFillColorDefaultBrush", app);
        Assert.Contains("CardStrokeColorDefaultBrush", app);
        Assert.DoesNotContain("FirstRunGuideCard", guide + app);
    }

    [Fact]
    public void MainWindowRootDoesNotReintroduceAnOpaqueBackground()
    {
        var root = FindRepositoryRoot();
        var window = File.ReadAllText(Path.Combine(root, "MainWindow.xaml"));
        Assert.DoesNotContain("RootLayout\" Background=\"{ThemeResource SolidBackgroundFillColorBaseBrush}\"", window);
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

    [Fact]
    public void LaunchPreflightsGuideStateBeforeSettingsAndUsesOneService()
    {
        var root = FindRepositoryRoot();
        var app = File.ReadAllText(Path.Combine(root, "App.xaml.cs"));
        var window = File.ReadAllText(Path.Combine(root, "MainWindow.xaml.cs"));
        var host = File.ReadAllText(Path.Combine(root, "Views", "FirstRunGuideHost.xaml.cs"));

        Assert.True(app.IndexOf("PrepareForLaunch", StringComparison.Ordinal) < app.IndexOf("new SettingsService().Load", StringComparison.Ordinal));
        Assert.Contains("FirstRunExperienceService.Default", window);
        Assert.Contains("FirstRunExperienceService.Default", host);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "UrbanPlanToolbox.slnx"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}
