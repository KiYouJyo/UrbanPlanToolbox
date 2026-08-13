using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class VisualPolishPackagingTests
{
    [Fact]
    public void VersionAndUserAgentAre110()
    {
        var root = FindRepositoryRoot();
        Assert.Contains("Version=\"1.5.10.0\"", File.ReadAllText(Path.Combine(root, "Package.appxmanifest")));
        Assert.Contains("<Version>1.5.10</Version>", File.ReadAllText(Path.Combine(root, "UrbanPlanToolbox.csproj")));
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
    public void StoreManifestUsesOfficialIdentityAndTechnicalVersion()
    {
        var root = FindRepositoryRoot();
        var manifest = File.ReadAllText(Path.Combine(root, "Package.Store.appxmanifest"));
        Assert.Contains("Name=\"JoKiy.UrbanPlanToolbox\"", manifest);
        Assert.Contains("Publisher=\"CN=C4E4B33A-7B77-4121-897C-7D720A5471F8\"", manifest);
        Assert.Contains("Version=\"1.5.10.0\"", manifest);
        Assert.Contains("<PublisherDisplayName>Jo Kiyō</PublisherDisplayName>", manifest);
        Assert.DoesNotContain("556F80C5-C4D4-452B-93B4-00DE3FA7AC29", manifest, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PhoneIdentity", manifest, StringComparison.Ordinal);

        var project = File.ReadAllText(Path.Combine(root, "UrbanPlanToolbox.csproj"));
        Assert.Contains("'$(DistributionChannel)' == 'Store'", project);
        Assert.Contains("Package.Store.appxmanifest", project);
    }

    [Fact]
    public void SharedVisualResourcesUseTheExistingStartupArtworkOverlay()
    {
        var root = FindRepositoryRoot();
        var app = File.ReadAllText(Path.Combine(root, "App.xaml"));
        Assert.Contains("PageContentStackPanelStyle", app);
        Assert.Contains("CardActionButtonStyle", app);
        Assert.Contains("StartupOverlay", File.ReadAllText(Path.Combine(root, "MainWindow.xaml")));
        Assert.Contains("Assets/SplashScreen.scale-200.png", File.ReadAllText(Path.Combine(root, "MainWindow.xaml")));
        var about = File.ReadAllText(Path.Combine(root, "Views", "AboutPage.xaml"));
        var aboutCode = File.ReadAllText(Path.Combine(root, "Views", "AboutPage.xaml.cs"));
        Assert.DoesNotContain("PackageIdentityText", about);
        Assert.DoesNotContain("About_PackageIdentityLabel", about);
        Assert.DoesNotContain("Package.Current.Id.FullName", aboutCode);
    }

    [Fact]
    public void AboutDiagnosticsUseTheSameCardContainerAndLocalizedResources()
    {
        var root = FindRepositoryRoot();
        var about = File.ReadAllText(Path.Combine(root, "Views", "AboutPage.xaml"));
        var aboutCode = File.ReadAllText(Path.Combine(root, "Views", "AboutPage.xaml.cs"));
        Assert.True(System.Text.RegularExpressions.Regex.Matches(about, "SettingsSectionCardStyle").Count >= 5);
        Assert.Contains("About_DiagnosticsTitle", about);
        Assert.Contains("About_DiagnosticsSummary", about);
        Assert.Contains("CopyDiagnosticsButton", about);
        Assert.Contains("OpenLogsButton", about);
        Assert.DoesNotContain("AboutContent.Children.Add", aboutCode);
        foreach (var language in new[] { "en-US", "zh-CN", "ja-JP" })
        {
            var resources = File.ReadAllText(Path.Combine(root, "Strings", language, "Resources.resw"));
            Assert.Contains("About_DiagnosticsTitle.Text", resources);
            Assert.Contains("About_DiagnosticsSummary.Text", resources);
        }
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "UrbanPlanToolbox.slnx"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}
