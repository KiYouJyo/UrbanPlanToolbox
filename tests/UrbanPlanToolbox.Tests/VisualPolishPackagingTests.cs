using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class VisualPolishPackagingTests
{
    [Fact]
    public void AppOwnedTransientSurfacesUseSharedResourcesAndCentralDialogPresentation()
    {
        var root = FindRepositoryRoot();
        var app = File.ReadAllText(Path.Combine(root, "App.xaml"));
        var dialogs = File.ReadAllText(Path.Combine(root, "Services", "AppDialogService.cs"));
        var comboBoxTheme = File.ReadAllText(Path.Combine(root, "Controls", "TransientComboBoxTheme.cs"));
        Assert.Contains("AppTransientSurfaceBrush", app);
        Assert.Contains("AppTransientSurfaceBorderBrush", app);
        Assert.Contains("ComboBoxDropDownBackground", app);
        Assert.Contains("ComboBoxDropDownBorderBrush", app);
        Assert.Matches("<StaticResource\\s+x:Key=\\\"ComboBoxDropDownBackground\\\"\\s+ResourceKey=\\\"AppTransientSurfaceBrush\\\"\\s*/>", app);
        Assert.Matches("<StaticResource\\s+x:Key=\\\"ComboBoxDropDownBorderBrush\\\"\\s+ResourceKey=\\\"CardStrokeColorDefaultBrush\\\"\\s*/>", app);
        Assert.Contains("dialog.Background =", dialogs);
        Assert.Contains("dialog.BorderBrush =", dialogs);
        Assert.Contains("Resources[\"ComboBoxDropDownBackground\"]", comboBoxTheme);
        Assert.Contains("Resources[\"ComboBoxDropDownBorderBrush\"]", comboBoxTheme);
        Assert.Contains("ActualThemeChanged", comboBoxTheme);
        Assert.DoesNotContain("ControlTemplate", comboBoxTheme);
        Assert.DoesNotContain("OverlayCornerRadius", comboBoxTheme);
        Assert.DoesNotContain("TargetType=\"ContentDialog\"", app);
        Assert.DoesNotContain("ControlTemplate TargetType=\"ContentDialog\"", app);
        Assert.DoesNotContain("ControlTemplate TargetType=\"Button\"", app);
        Assert.DoesNotContain("ControlTemplate TargetType=\"ComboBox\"", app);
        Assert.DoesNotContain("ControlCornerRadius", app);
        Assert.DoesNotContain("ButtonCornerRadius", app);
        Assert.DoesNotContain("ContentDialogCornerRadius", app);
        Assert.DoesNotContain("OverlayCornerRadius", app);
        Assert.DoesNotContain("ComboBoxItemBackground", app);
        foreach (var source in Directory.EnumerateFiles(Path.Combine(root, "Views"), "*.xaml", SearchOption.AllDirectories))
        {
            var xaml = File.ReadAllText(source);
            Assert.Equal(
                System.Text.RegularExpressions.Regex.Matches(xaml, "<ComboBox(?:\\s|>)").Count,
                System.Text.RegularExpressions.Regex.Matches(xaml, "<ComboBox\\s+controls:TransientComboBoxTheme.Apply=\\\"True\\\"").Count);
        }
        foreach (var source in Directory.EnumerateFiles(Path.Combine(root, "Views"), "*.cs", SearchOption.AllDirectories))
        {
            var code = File.ReadAllText(source);
            Assert.DoesNotContain(".ShowAsync()", code);
            Assert.Equal(
                System.Text.RegularExpressions.Regex.Matches(code, "new ComboBox").Count,
                System.Text.RegularExpressions.Regex.Matches(code, "TransientComboBoxTheme.ApplyTo").Count);
        }
    }

    [Fact]
    public void VersionAndUserAgentAre175()
    {
        var root = FindRepositoryRoot();
        Assert.Contains("Version=\"1.7.5.0\"", File.ReadAllText(Path.Combine(root, "Package.appxmanifest")));
        Assert.Contains("<Version>1.7.5</Version>", File.ReadAllText(Path.Combine(root, "UrbanPlanToolbox.csproj")));
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
        Assert.Contains("Version=\"1.7.5.0\"", manifest);
        Assert.Contains("<PublisherDisplayName>Jo Kiyō</PublisherDisplayName>", manifest);
        Assert.DoesNotContain("556F80C5-C4D4-452B-93B4-00DE3FA7AC29", manifest, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PhoneIdentity", manifest, StringComparison.Ordinal);

        var project = File.ReadAllText(Path.Combine(root, "UrbanPlanToolbox.csproj"));
        Assert.Contains("'$(DistributionChannel)' == 'Store'", project);
        Assert.Contains("Package.Store.appxmanifest", project);
    }

    [Fact]
    public void CreateProjectDialogUsesAdaptiveWideSharedFormLayout()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "Views", "HomePage.xaml.cs"));

        Assert.Single(System.Text.RegularExpressions.Regex.Matches(source, "private async Task ShowCreateDialogAsync\\(string kind\\)").Cast<System.Text.RegularExpressions.Match>());
        Assert.Contains("const double dialogMaxWidth = 760;", source);
        Assert.Contains("XamlRoot.Size.Width - dialogOuterMargin", source);
        Assert.Contains("effectiveDialogMinWidth = Math.Min(dialogMinWidth, dialogWidth)", source);
        Assert.Contains("Width = dialogWidth", source);
        Assert.Contains("MinWidth = effectiveDialogMinWidth", source);
        Assert.Contains("MaxWidth = dialogMaxWidth", source);
        Assert.Contains("const double scrollBarGutter = 20;", source);
        Assert.Contains("new ColumnDefinition { Width = new GridLength(scrollBarGutter) }", source);
        Assert.Contains("VerticalScrollBarVisibility = ScrollBarVisibility.Auto", source);
        Assert.Contains("HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled", source);
        Assert.Contains("HorizontalContentAlignment = HorizontalAlignment.Stretch", source);
        Assert.Contains("Grid.SetColumn(panel, 0);", source);
        Assert.Contains("HorizontalAlignment = HorizontalAlignment.Stretch", source);
        Assert.Contains("CreateResearchAsync(name.Text, selected.Code, customType.Text, field!.Text, subject!.Text, methods!.Text)", source);
        Assert.Contains("CreateAsync(name.Text, selected.Code, customType.Text, area!.Text, lat, lon, description!.Text, requirements!.Text)", source);
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
        Assert.Contains("x:Name=\"ProductLogo\"", about);
        Assert.DoesNotContain("Square44x44Logo.scale-200.png", about);
        Assert.Contains("ActualThemeChanged += OnActualThemeChanged", aboutCode);
        Assert.Contains("WindowIconTheme.GetLogoUri(theme)", aboutCode);
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
    public void WindowChromeAndShellCandidatesFollowTheirSeparateThemeSources()
    {
        var root = FindRepositoryRoot();
        var window = File.ReadAllText(Path.Combine(root, "MainWindow.xaml.cs"));
        var project = File.ReadAllText(Path.Combine(root, "UrbanPlanToolbox.csproj"));

        Assert.Contains("WindowIconTheme.Resolve", window);
        Assert.Contains("AppWindow.SetIcon(iconPath)", window);
        Assert.Contains("AppTitleBar.IconSource", window);
        Assert.Contains("PreferredTheme", window);
        Assert.Contains("ColorValuesChanged", window);
        Assert.Contains("WindowIcon-ForDarkTheme.ico", project);
        Assert.Contains("WindowIcon-ForLightTheme.ico", project);

        foreach (var size in new[] { 16, 20, 24, 30, 32, 36, 40, 44, 48, 60, 64, 72, 80, 96, 256 })
        {
            var darkShell = Path.Combine(root, "Assets", $"Square44x44Logo.targetsize-{size}_altform-unplated.png");
            var lightShell = Path.Combine(root, "Assets", $"Square44x44Logo.targetsize-{size}_altform-unplated_theme-light.png");
            Assert.True(File.Exists(darkShell), $"Missing default/Dark Shell candidate: {Path.GetFileName(darkShell)}");
            Assert.True(File.Exists(lightShell), $"Missing Light Shell candidate: {Path.GetFileName(lightShell)}");
            Assert.Equal(ReadPngDimensions(darkShell), ReadPngDimensions(lightShell));
        }

        Assert.True(File.Exists(Path.Combine(root, "Assets", "WindowIcon-ForDarkTheme.ico")));
        Assert.True(File.Exists(Path.Combine(root, "Assets", "WindowIcon-ForLightTheme.ico")));
    }

    [Fact]
    public void ThemeAssetConventionDocumentsAppAndShellMappings()
    {
        var root = FindRepositoryRoot();
        var convention = File.ReadAllText(Path.Combine(root, "docs", "ASSET-CONVENTIONS.md"));
        var mapping = File.ReadAllText(Path.Combine(root, "Services", "WindowIconTheme.cs"));
        var generator = File.ReadAllText(Path.Combine(root, "scripts", "Generate-ThemeIconAssets.ps1"));

        foreach (var required in new[] { "Theme names describe the target environment", "App Dark Theme", "App Light Theme", "Windows Shell Dark Theme", "Windows Shell Light Theme", "theme-light", "ForDarkShellTheme", "ForLightShellTheme", "Dark target environment", "Light target environment" })
            Assert.Contains(required, convention);
        Assert.Contains("IconForDarkThemeRelativePath", mapping);
        Assert.Contains("IconForLightThemeRelativePath", mapping);
        Assert.Contains("WhiteIconSourceRelativePath", mapping);
        Assert.Contains("BlackIconSourceRelativePath", mapping);
        Assert.Contains("$forDarkShellTheme", generator);
        Assert.Contains("$forLightShellTheme", generator);
        Assert.Contains("$blackSource", generator);
    }

    [Fact]
    public void NavigationPaneUsesTheSharedShellSurface()
    {
        var root = FindRepositoryRoot();
        var page = File.ReadAllText(Path.Combine(root, "MainPage.xaml"));
        var app = File.ReadAllText(Path.Combine(root, "App.xaml"));
        Assert.Contains("ShellNavigationPaneBackgroundBrush", page + File.ReadAllText(Path.Combine(root, "MainPage.xaml.cs")));
        Assert.Contains("Navigation.ActualTheme == ElementTheme.Dark", File.ReadAllText(Path.Combine(root, "MainPage.xaml.cs")));
        Assert.DoesNotContain("Windows.UI.Color.FromArgb", File.ReadAllText(Path.Combine(root, "MainPage.xaml.cs")));
        Assert.Contains("x:Key=\"ShellNavigationPaneBackgroundBrush\"", app);
        Assert.DoesNotContain("FirstRunGuideBackgroundBrush", app);
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
