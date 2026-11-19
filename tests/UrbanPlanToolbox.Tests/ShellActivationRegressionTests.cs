using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class ShellActivationRegressionTests
{
    [Fact]
    public void NavigationPaneUsesAcrylicBackedTransparentThemeInsteadOfActivationColors()
    {
        var root = FindRepositoryRoot();
        var app = File.ReadAllText(Path.Combine(root, "App.xaml"));
        var shell = File.ReadAllText(Path.Combine(root, "MainPage.xaml.cs"));
        var navigation = File.ReadAllText(Path.Combine(root, "MainPage.xaml"));
        var window = File.ReadAllText(Path.Combine(root, "MainWindow.xaml"));

        const string transparentBrush = "<SolidColorBrush x:Key=\"NavigationViewExpandedPaneBackground\" Color=\"Transparent\" />";
        Assert.Equal(2, app.Split(transparentBrush, StringSplitOptions.None).Length - 1);
        Assert.Contains("NavigationViewExpandedPaneBackground\" Color=\"{ThemeResource SystemColorWindowColor}\"", app);
        Assert.Contains("<NavigationView x:Name=\"Navigation\" Background=\"Transparent\"", navigation);
        Assert.Contains("<DesktopAcrylicBackdrop", window);
        Assert.DoesNotContain("ShellNavigationPaneBackgroundBrush", app + shell);
        Assert.DoesNotContain("ShellNavigationPaneInactiveBackgroundBrush", app + shell);
        Assert.DoesNotContain("PaneBackground =", shell);
        Assert.DoesNotContain("App.MainWindow.Activated += OnMainWindowActivated", shell);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "UrbanPlanToolbox.slnx"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}
