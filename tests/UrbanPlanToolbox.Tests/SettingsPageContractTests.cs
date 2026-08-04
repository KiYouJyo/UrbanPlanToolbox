using System.Text.RegularExpressions;
using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class SettingsPageContractTests
{
    [Fact]
    public void SettingsPageUsesCardsResponsiveRowsAndExistingControlsOnce()
    {
        var root = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "Views", "SettingsPage.xaml"));
        Assert.True(Regex.Matches(xaml, "SettingsSectionCardStyle").Count >= 3);
        Assert.Contains("AdaptiveTrigger MinWindowWidth=\"720\"", xaml);
        foreach (var name in new[] { "ThemeBox", "LanguageBox", "DecimalBox", "AutoCalculateToggle", "ExportButton", "ImportButton", "ClearDataButton" })
            Assert.Single(Regex.Matches(xaml, $"x:Name=\"{name}\"").Cast<Match>());
        Assert.DoesNotContain("Canvas", xaml);
        Assert.DoesNotContain("Margin=\"-", xaml);
    }

    [Fact]
    public void LanguageSelectionHasInitializationGuardAndLocalizedRestartDialog()
    {
        var root = FindRepositoryRoot();
        var code = File.ReadAllText(Path.Combine(root, "Views", "SettingsPage.xaml.cs"));
        Assert.Contains("if (_isApplying) return;", code);
        Assert.Contains("LanguageRestartPromptCoordinator", code);
        foreach (var key in new[] { "Setting_Language_RestartTitle", "Setting_Language_RestartMessage", "Setting_Language_RestartNow", "Setting_Language_Later", "Setting_Language_RestartFailed" })
            Assert.Contains($"GetString(\"{key}\")", code);
        Assert.True(code.IndexOf("_settingsService.Update(current => current.Language", StringComparison.Ordinal) < code.IndexOf("ShowAsync(dialog)", StringComparison.Ordinal));
        Assert.DoesNotContain("AppInstance.Restart", code);
        Assert.Contains("AppInstance.Restart", File.ReadAllText(Path.Combine(root, "Services", "ApplicationRestartService.cs")));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "UrbanPlanToolbox.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
