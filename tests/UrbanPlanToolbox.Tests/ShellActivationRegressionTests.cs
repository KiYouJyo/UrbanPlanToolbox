using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class ShellActivationRegressionTests
{
    [Fact]
    public void NavigationPaneUsesExplicitActiveAndInactiveThemeSurfaces()
    {
        var root = FindRepositoryRoot();
        var app = File.ReadAllText(Path.Combine(root, "App.xaml"));
        var shell = File.ReadAllText(Path.Combine(root, "MainPage.xaml.cs"));

        Assert.Contains("ShellNavigationPaneBackgroundBrush", app);
        Assert.Contains("ShellNavigationPaneInactiveBackgroundBrush", app);
        Assert.Contains("Color=\"#E5F9F9\"", app);
        Assert.Contains("Color=\"#1A2323\"", app);
        Assert.Contains("Color=\"#F3F3F3\"", app);
        Assert.Contains("Color=\"#202020\"", app);
        Assert.Contains("SystemColorWindowBrush", app);

        Assert.Contains("App.MainWindow.Activated += OnMainWindowActivated", shell);
        Assert.Contains("App.MainWindow.Activated -= OnMainWindowActivated", shell);
        Assert.Contains("args.WindowActivationState != WindowActivationState.Deactivated", shell);
        Assert.Contains("_isWindowActive", shell);
        Assert.Contains("? \"ShellNavigationPaneBackgroundBrush\"", shell);
        Assert.Contains(": \"ShellNavigationPaneInactiveBackgroundBrush\"", shell);
        Assert.Contains("ActualThemeChanged", shell);
        Assert.Contains("Navigation.ActualTheme == ElementTheme.Dark", shell);
        Assert.Contains("themeResources?[brushKey]", shell);
    }

    [Fact]
    public void NativeMicaRemainsVisibleThroughTheEntireShellContentPath()
    {
        var root = FindRepositoryRoot();
        var windowXaml = File.ReadAllText(Path.Combine(root, "MainWindow.xaml"));
        var pageXaml = File.ReadAllText(Path.Combine(root, "MainPage.xaml"));

        Assert.Contains("<MicaBackdrop />", windowXaml);
        Assert.DoesNotContain("WindowChromeThemeBehavior", windowXaml);
        Assert.DoesNotContain("TitleBarSurface", windowXaml);
        Assert.Contains("<TitleBar x:Name=\"AppTitleBar\" Title=\"UrbanPlanToolbox\" Background=\"Transparent\">", windowXaml);
        Assert.Contains("<Frame x:Name=\"RootFrame\" Grid.Row=\"1\" Background=\"Transparent\" />", windowXaml);
        Assert.Contains("Background=\"Transparent\"", pageXaml);
        Assert.Contains("<Frame x:Name=\"ContentFrame\" Background=\"Transparent\" />", pageXaml);
        Assert.False(File.Exists(Path.Combine(root, "WindowChromeThemeBehavior.cs")));
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "UrbanPlanToolbox.slnx"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}
