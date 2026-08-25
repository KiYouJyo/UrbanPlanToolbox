using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class ShellActivationRegressionTests
{
    [Fact]
    public void NavigationPaneKeepsThemeSurfaceWhenMainWindowDeactivates()
    {
        var root = FindRepositoryRoot();
        var app = File.ReadAllText(Path.Combine(root, "App.xaml"));
        var shell = File.ReadAllText(Path.Combine(root, "MainPage.xaml.cs"));

        Assert.Contains("ShellNavigationPaneBackgroundBrush", app);
        Assert.DoesNotContain("ShellNavigationPaneInactiveBackgroundBrush", app);
        Assert.Contains("Color=\"#E5F9F9\"", app);
        Assert.Contains("Color=\"#1A2323\"", app);
        Assert.DoesNotContain("Color=\"#F3F3F3\"", app);
        Assert.DoesNotContain("Color=\"#202020\"", app);

        Assert.DoesNotContain("App.MainWindow.Activated += OnMainWindowActivated", shell);
        Assert.DoesNotContain("_isWindowActive", shell);
        Assert.DoesNotContain("ShellNavigationPaneInactiveBackgroundBrush", shell);
        Assert.Contains("ActualThemeChanged", shell);
        Assert.Contains("Navigation.ActualTheme == ElementTheme.Dark", shell);
        Assert.Contains("themeResources?[\"ShellNavigationPaneBackgroundBrush\"]", shell);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "UrbanPlanToolbox.slnx"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}
