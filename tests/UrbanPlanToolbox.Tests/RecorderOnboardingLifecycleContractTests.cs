using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class RecorderOnboardingLifecycleContractTests
{
    [Fact]
    public void FirstRunGuideBackgroundToggleUsesTheSharedRecorderLifecycle()
    {
        var code = Read("Views", "FirstRunGuideHost.xaml.cs");

        Assert.Contains("App.ApplyBackgroundResidency(settings.BackgroundResidencyEnabled)", code);
        Assert.Contains("await App.ShowInspirationRecorderAsync(moveToPrimaryWorkAreaTopRight: true)", code);
        Assert.DoesNotContain("new InspirationRecorderWindow", code);
        Assert.Contains("_isBusy = true", code);
        Assert.Contains("finally", code);
    }

    [Fact]
    public void SilentStartupOnAutomaticallyEnablesResidencyAndShowsRecorder()
    {
        var code = Read("Views", "FirstRunGuideHost.xaml.cs");

        Assert.Contains("if (requested) s.BackgroundResidencyEnabled = true", code);
        Assert.Contains("if (requested) await App.ShowInspirationRecorderAsync(moveToPrimaryWorkAreaTopRight: true)", code);
        Assert.Contains("BackgroundResidencyToggle.IsOn = settings.BackgroundResidencyEnabled", code);
    }

    [Fact]
    public void RecorderCloseGlyphUsesNativeCaptionThemeColorsWithoutXamlHardCoding()
    {
        var code = Read("Views", "InspirationRecorderWindow.xaml.cs");
        var xaml = Read("Views", "InspirationRecorderWindow.xaml");

        Assert.Contains("AppWindow.TitleBar.ButtonForegroundColor", code);
        Assert.Contains("AppWindow.TitleBar.ButtonInactiveForegroundColor", code);
        Assert.Contains("AppWindow.TitleBar.ButtonHoverForegroundColor", code);
        Assert.Contains("AppWindow.TitleBar.ButtonPressedForegroundColor", code);
        Assert.Contains("WindowIconTheme.Resolve", code);
        Assert.Contains("ColorValuesChanged", code);
        Assert.DoesNotContain("Foreground=\"Black\"", xaml);
        Assert.DoesNotContain("Foreground=\"White\"", xaml);
    }

    private static string Read(params string[] segments)
    {
        var root = FindRepositoryRoot();
        return File.ReadAllText(Path.Combine([root, .. segments]));
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "UrbanPlanToolbox.slnx"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}
