using System.Text.RegularExpressions;
using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class SettingsPageContractTests
{
    [Fact]
    public void SettingsPageMatchesFigmaWideLayoutAndKeepsCompactFallback()
    {
        var root = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "Views", "SettingsPage.xaml"));
        Assert.True(Regex.Matches(xaml, "SettingsSectionCardStyle").Count >= 5);
        Assert.Contains("AdaptiveTrigger MinWindowWidth=\"520\"", xaml);
        Assert.Contains("x:Name=\"SettingsLayoutRoot\"", xaml);
        Assert.Contains("x:Name=\"SettingsCompact\"", xaml);
        Assert.Contains("x:Name=\"SettingsWide\"", xaml);
        Assert.Single(Regex.Matches(xaml, "<VisualStateManager.VisualStateGroups>").Cast<Match>());
        Assert.Matches("x:Name=\"SettingsContent\"[\\s\\S]*?HorizontalAlignment=\"Left\"[\\s\\S]*?MaxWidth=\"1088\"", xaml);
        foreach (var name in new[] { "ThemeBox", "LanguageBox", "ExportButton", "ImportButton", "ClearDataButton", "ApplicationSettingsTitle" })
            Assert.Single(Regex.Matches(xaml, $"x:Name=\"{name}\"").Cast<Match>());
        foreach (var name in new[] { "ThemeBox", "LanguageBox" })
            Assert.Matches($"x:Name=\"{name}\"[\\s\\S]*?Grid.Row=\"0\"[\\s\\S]*?Grid.Column=\"1\"[\\s\\S]*?Width=\"250\"[\\s\\S]*?HorizontalAlignment=\"Right\"", xaml);
        foreach (var name in new[] { "BackgroundResidencyToggle", "SilentStartupToggle", "MilestoneNotificationsToggle", "RestoreDefaultsButton", "ReopenFirstRunGuideButton" })
            Assert.Matches($"x:Name=\"{name}\"[\\s\\S]*?Grid.Row=\"0\"[\\s\\S]*?Grid.Column=\"1\"[\\s\\S]*?HorizontalAlignment=\"Right\"", xaml);
        foreach (var name in new[] { "AppearanceLanguageCard", "ResidencyCard", "MilestoneNotificationsCard", "DataManagementCard", "ApplicationMaintenanceCard" })
            Assert.Contains($"x:Name=\"{name}\"", xaml);
        Assert.Matches("x:Name=\"DataLocalPanel\"[\\s\\S]*?Grid.Row=\"0\"[\\s\\S]*?Grid.Column=\"0\"[\\s\\S]*?Grid.ColumnSpan=\"1\"", xaml);
        Assert.Matches("x:Name=\"DataCloudPanel\"[\\s\\S]*?Grid.Row=\"0\"[\\s\\S]*?Grid.Column=\"1\"[\\s\\S]*?Grid.ColumnSpan=\"1\"", xaml);
        Assert.Contains("ControlFillColorDefaultBrush", xaml);
        Assert.DoesNotContain("AccentButtonStyle", xaml);
        Assert.DoesNotContain("DisplayCalculationTitle", xaml);
        Assert.DoesNotContain("DecimalBox", xaml);
        Assert.DoesNotContain("AutoCalculateToggle", xaml);
        Assert.DoesNotContain("AppearanceLanguageSummary", xaml);
        Assert.Contains("Action_RestoreDefaults", xaml);
        Assert.Contains("DataManagementTitle", xaml);
        Assert.Contains("MilestoneNotificationsToggle", xaml);
        Assert.DoesNotContain("Canvas", xaml);
        Assert.DoesNotContain("Margin=\"-", xaml);
    }

    [Fact]
    public void MilestoneReminderIsApplicationScopedAndEditorHasNoPerItemToggle()
    {
        var root = FindRepositoryRoot();
        var code = File.ReadAllText(Path.Combine(root, "Views", "ProjectWorkspacePage.xaml.cs"));
        var xaml = File.ReadAllText(Path.Combine(root, "Views", "SettingsPage.xaml"));
        var settings = File.ReadAllText(Path.Combine(root, "Views", "SettingsPage.xaml.cs"));
        Assert.DoesNotContain("Milestone_Field_Reminder", code);
        Assert.DoesNotContain("ReminderEnabled", code);
        Assert.Contains("SetEnabledAsync", settings);
        Assert.Contains("GetSettingsAsync", settings);
        Assert.Contains("UpdateRepeatIntervalAsync", settings);
        Assert.Contains("MilestoneNotificationsRepeatBox", xaml);
        Assert.Contains("Settings_MilestoneNotificationsRepeatHours6", xaml);
        Assert.Contains("Settings_MilestoneNotificationsRepeatDays3", xaml);
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
        Assert.Contains("SwitchLanguageAsync", code);
    }

    [Fact]
    public void LanguageSelectionHasInitializationGuardAndLocalizedRestartDialog()
    {
        var root = FindRepositoryRoot();
        var code = File.ReadAllText(Path.Combine(root, "Views", "SettingsPage.xaml.cs"));
        Assert.Contains("if (_isApplying) return;", code);
        Assert.Contains("SwitchLanguageAsync", code);
        Assert.Contains("LanguageBox.IsEnabled = false", code);
        Assert.DoesNotContain("ShowLanguageRestartDialogAsync", code);
        Assert.DoesNotContain("AppInstance.Restart", code);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "UrbanPlanToolbox.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}