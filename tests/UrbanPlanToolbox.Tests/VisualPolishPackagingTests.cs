using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class VisualPolishPackagingTests
{
    [Fact]
    public void VersionAndUserAgentAre040()
    {
        var root = FindRepositoryRoot();
        Assert.Contains("Version=\"0.4.1.0\"", File.ReadAllText(Path.Combine(root, "Package.appxmanifest")));
        Assert.Contains("<Version>0.4.1</Version>", File.ReadAllText(Path.Combine(root, "UrbanPlanToolbox.csproj")));
        Assert.Contains("UrbanPlanToolbox/", File.ReadAllText(Path.Combine(root, "Services", "GitHubUpdateService.cs")));
    }

    [Fact]
    public void NativeSplashScreenIsDeclaredAndIncludedAsContent()
    {
        var root = FindRepositoryRoot();
        var manifest = File.ReadAllText(Path.Combine(root, "Package.appxmanifest"));
        Assert.Contains("<uap:SplashScreen Image=\"Assets\\SplashScreen.png\" BackgroundColor=\"#202020\"", manifest);
        Assert.Contains("Assets\\SplashScreen.scale-*.png", File.ReadAllText(Path.Combine(root, "UrbanPlanToolbox.csproj")));
        foreach (var scale in new[] { 100, 125, 150, 200, 400 })
            Assert.True(File.Exists(Path.Combine(root, "Assets", $"SplashScreen.scale-{scale}.png")));
    }

    [Fact]
    public void SharedVisualResourcesAreUsedWithoutAnInAppSplashOverlay()
    {
        var root = FindRepositoryRoot();
        var app = File.ReadAllText(Path.Combine(root, "App.xaml"));
        Assert.Contains("PageContentStackPanelStyle", app);
        Assert.Contains("CardActionButtonStyle", app);
        Assert.DoesNotContain("Splash", File.ReadAllText(Path.Combine(root, "App.xaml.cs")), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Splash", File.ReadAllText(Path.Combine(root, "MainWindow.xaml")), StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "UrbanPlanToolbox.slnx"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}
