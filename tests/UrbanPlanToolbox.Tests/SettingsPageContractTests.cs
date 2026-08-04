using System.Text.RegularExpressions;
using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class SettingsPageContractTests
{
    [Fact]
    public void SettingsPageIsLeftAlignedCompactAndOmitsToolSpecificOptions()
    {
        var root = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "Views", "SettingsPage.xaml"));
        Assert.True(Regex.Matches(xaml, "SettingsSectionCardStyle").Count >= 3);
        Assert.Contains("AdaptiveTrigger MinWindowWidth=\"720\"", xaml);
        Assert.Contains("x:Name=\"SettingsLayoutRoot\"", xaml);
        Assert.Contains("x:Name=\"SettingsNarrow\"", xaml);
        Assert.Contains("x:Name=\"SettingsWide\"", xaml);
        Assert.Single(Regex.Matches(xaml, "<VisualStateManager.VisualStateGroups>").Cast<Match>());
        Assert.Matches("x:Name=\"SettingsContent\"[\\s\\S]*?HorizontalAlignment=\"Left\"[\\s\\S]*?MaxWidth=\"900\"", xaml);
        foreach (var name in new[] { "ThemeBox", "LanguageBox", "ExportButton", "ImportButton", "ClearDataButton", "ApplicationSettingsTitle" })
            Assert.Single(Regex.Matches(xaml, $"x:Name=\"{name}\"").Cast<Match>());
        foreach (var name in new[] { "ThemeBox", "LanguageBox" })
            Assert.Matches($"x:Name=\"{name}\"[\\s\\S]*?MinWidth=\"240\"[\\s\\S]*?MaxWidth=\"360\"[\\s\\S]*?HorizontalAlignment=\"Left\"", xaml);
        Assert.DoesNotContain("DisplayCalculationTitle", xaml);
        Assert.DoesNotContain("DecimalBox", xaml);
        Assert.DoesNotContain("AutoCalculateToggle", xaml);
        Assert.DoesNotContain("AppearanceLanguageSummary", xaml);
        Assert.Contains("Action_RestoreDefaults", xaml);
        Assert.Contains("DataManagementTitle", xaml);
        Assert.DoesNotContain("Canvas", xaml);
        Assert.DoesNotContain("Margin=\"-", xaml);
    }

    [Fact]
    public void RemovingToolSpecificControlsKeepsLegacySettingsAndRestoreBoundary()
    {
        var root = FindRepositoryRoot();
        var code = File.ReadAllText(Path.Combine(root, "Views", "SettingsPage.xaml.cs"));
        var model = File.ReadAllText(Path.Combine(root, "Models", "AppSettings.cs"));
        Assert.Contains("current.DecimalPlaces = 2", code);
        Assert.Contains("current.AutoCalculate = false", code);
        Assert.Contains("public int DecimalPlaces", model);
        Assert.Contains("public bool AutoCalculate", model);
        var restore = code[code.IndexOf("private async void OnRestore", StringComparison.Ordinal)..code.IndexOf("private void OnThemeChanged", StringComparison.Ordinal)];
        Assert.DoesNotContain("Delete", restore);
        Assert.DoesNotContain("ClearLocalData", restore);
        Assert.Contains("ShowLanguageRestartDialogAsync", restore);
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
