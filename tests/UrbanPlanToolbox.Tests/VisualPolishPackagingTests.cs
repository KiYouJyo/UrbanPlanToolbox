using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class VisualPolishPackagingTests
{
    [Fact]
    public void VersionAndUserAgentAre166()
    {
        var root = FindRepositoryRoot();
        Assert.Contains("Version=\"1.6.6.0\"", File.ReadAllText(Path.Combine(root, "Package.appxmanifest")));
        Assert.Contains("<Version>1.6.6</Version>", File.ReadAllText(Path.Combine(root, "UrbanPlanToolbox.csproj")));
        Assert.Contains("UrbanPlanToolbox/", File.ReadAllText(Path.Combine(root, "Services", "GitHubUpdateService.cs")));
    }

    [Fact]
    public void NativeSplashScreenIsDeclaredAndIncludedAsContent()
    {
        var root = FindRepositoryRoot();
        var manifest = File.ReadAllText(Path.Combine(root, "Package.appxmanifest"));
        Assert.Contains("xmlns:uap5=\"http://schemas.microsoft.com/appx/manifest/uap/windows10/5\"", manifest);
        Assert.Contains("uap5:Optional=\"true\"", manifest);
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
        Assert.Contains("Version=\"1.6.6.0\"", manifest);
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
        var window = File.ReadAllText(Path.Combine(root, "MainWindow.xaml"));
        Assert.Contains("StartupOverlay\" Background=\"Transparent\"", window);
        Assert.Contains("MainContent\" Opacity=\"0\" IsHitTestVisible=\"False\"", window);
        Assert.Contains("StartupDarkLogo\"", window);
        Assert.Contains("Source=\"ms-appx:///Assets/Icon-Large-Dark-1024.png\"", window);
        Assert.Contains("StartupLightLogo\"", window);
        Assert.Contains("Source=\"ms-appx:///Assets/Icon-Large-Light-1024.png\"", window);
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(window, "Width=\"183\"").Count);
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(window, "Height=\"183\"").Count);
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(window, "Stretch=\"Uniform\"").Count);
        Assert.DoesNotContain("ApplicationPageBackgroundThemeBrush", window);
        var windowCode = File.ReadAllText(Path.Combine(root, "MainWindow.xaml.cs"));
        Assert.Contains("_selectedStartupLogo", windowCode);
        Assert.DoesNotContain("StartupDarkLogo.Width", windowCode);
        Assert.DoesNotContain("StartupLightLogo.Width", windowCode);
        Assert.DoesNotContain("StartupDarkLogo.Height", windowCode);
        Assert.DoesNotContain("StartupLightLogo.Height", windowCode);
        Assert.Contains("MainContent.Opacity = 1", windowCode);
        Assert.Contains("DoubleAnimation", windowCode);
        Assert.Contains("StartupSplashTiming.FadeOutDuration", windowCode);
        Assert.Contains("CubicEase { EasingMode = EasingMode.EaseOut }", windowCode);
        Assert.Contains("Startup.FadeStarted", windowCode);
        Assert.Contains("Startup.FadeCompleted", windowCode);
        Assert.Contains("Startup.FadeFallback", windowCode);
        Assert.Contains("StartupWatchdogTriggered", windowCode);
        Assert.Contains("CompositionTarget.Rendering", windowCode);
        Assert.Contains("Task.Delay", windowCode);
        Assert.Contains("RestoreWindowPlacement", windowCode);
        Assert.Contains("AppWindow.Changed", windowCode);
        Assert.Contains("OverlappedPresenterState.Maximized", windowCode);
        Assert.Contains("OverlappedPresenterState.Minimized", windowCode);
        Assert.DoesNotContain("Thread.Sleep", windowCode);
        var about = File.ReadAllText(Path.Combine(root, "Views", "AboutPage.xaml"));
        var aboutCode = File.ReadAllText(Path.Combine(root, "Views", "AboutPage.xaml.cs"));
        Assert.DoesNotContain("PackageIdentityText", about);
        Assert.DoesNotContain("About_PackageIdentityLabel", about);
        Assert.DoesNotContain("Package.Current.Id.FullName", aboutCode);
    }

    [Fact]
    public void StartupThemeAssetsUseTheSameSquareHighResolutionCanvas()
    {
        var root = FindRepositoryRoot();
        var light = ReadPngDimensions(Path.Combine(root, "Assets", "Icon-Large-Light-1024.png"));
        var dark = ReadPngDimensions(Path.Combine(root, "Assets", "Icon-Large-Dark-1024.png"));
        Assert.Equal((1024, 1024), light);
        Assert.Equal(light, dark);
    }

    [Fact]
    public void OverlayPaneUsesTheActualThemeColorOnTheNavigationSplitView()
    {
        var root = FindRepositoryRoot();
        var page = File.ReadAllText(Path.Combine(root, "MainPage.xaml"));
        var pageCode = File.ReadAllText(Path.Combine(root, "MainPage.xaml.cs"));
        Assert.Contains("FindDescendant<SplitView>(Navigation)", pageCode);
        Assert.Contains("Navigation.ActualTheme == ElementTheme.Light", pageCode);
        Assert.Contains("Windows.UI.Color.FromArgb(0xFF, 0xE5, 0xF9, 0xF9)", pageCode);
        Assert.Contains("Windows.UI.Color.FromArgb(0xFF, 0x1A, 0x23, 0x23)", pageCode);
        Assert.Contains("Navigation.PaneOpening", pageCode);
        Assert.DoesNotContain("PaneDisplayMode=\"Left\"", page);
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

    private static (int Width, int Height) ReadPngDimensions(string path)
    {
        var header = new byte[24];
        using var stream = File.OpenRead(path);
        Assert.Equal(header.Length, stream.Read(header));
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, header[..8]);
        return (
            System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(16, 4)),
            System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(20, 4)));
    }
}
