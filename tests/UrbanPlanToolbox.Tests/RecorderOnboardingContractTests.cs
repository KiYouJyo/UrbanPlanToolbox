using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class RecorderOnboardingContractTests
{
    [Fact]
    public void RecorderIsTheFourthGuidePageAndGuideHasFivePages()
    {
        var root = FindRoot();
        var host = File.ReadAllText(Path.Combine(root, "Views", "FirstRunGuideHost.xaml.cs"));
        Assert.Contains("_step < 4", host);
        Assert.Contains("_step == 4", host);
        Assert.Contains("RecorderSettingsPanel.Visibility = _step == 3", host);
    }

    [Fact]
    public void SettingsAndOnboardingUseTheSameTwoBackgroundSettings()
    {
        var root = FindRoot();
        var settings = File.ReadAllText(Path.Combine(root, "Views", "SettingsPage.xaml.cs"));
        var guide = File.ReadAllText(Path.Combine(root, "Views", "FirstRunGuideHost.xaml.cs"));
        foreach (var name in new[] { "BackgroundResidencyEnabled", "SilentStartupShowRecorder" })
        {
            Assert.Contains(name, settings);
            Assert.Contains(name, guide);
        }
    }

    [Fact]
    public void EverySupportedLanguageShowsFiveGuidePages()
    {
        var root = FindRoot();
        foreach (var language in new[] { "zh-CN", "ja-JP", "en-US" })
        {
            var resources = File.ReadAllText(Path.Combine(root, "Strings", language, "Resources.resw"));
            Assert.Contains("<data name=\"FirstRunGuide_Step\" xml:space=\"preserve\"><value>{0} / 5</value></data>", resources);
        }
    }

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "UrbanPlanToolbox.slnx"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}
