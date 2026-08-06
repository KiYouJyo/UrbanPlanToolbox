using UrbanPlanToolbox.Helpers;
using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Services;
using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class SettingsAndFormattingTests
{
    [Theory]
    [InlineData(0, "1")]
    [InlineData(1, "1.2")]
    [InlineData(2, "1.23")]
    [InlineData(3, "1.235")]
    public void FormatsDecimalPlacesWithoutChangingRawValue(int places, string expected) => Assert.Equal(expected, NumberFormatter.Value(1.23456m, places));

    [Fact] public void FormatsPercentAndEmptyValue() { Assert.Equal("12.35%", NumberFormatter.Percent(12.345m, 2)); Assert.Equal("—", NumberFormatter.Value(null, 2)); }
    [Fact] public void FormattingDoesNotRoundUnderlyingResult() { var result = new PlanningResult { FloorAreaRatio = 1.23456m }; _ = PlanningResultFormatter.Format(result, 0, TestLocalization.ZhCn); Assert.Equal(1.23456m, result.FloorAreaRatio); }
    [Fact]
    public void SettingsRoundTripPersistsAutoCalculationAndPrecision()
    {
        var path = Path.Combine(Path.GetTempPath(), $"UrbanPlanToolbox-{Guid.NewGuid():N}", "settings.json");
        try { var service = new SettingsService(path); service.Save(new AppSettings { DecimalPlaces = 3, AutoCalculate = true, Theme = "Dark", Language = "ja-JP" }); var loaded = new SettingsService(path).Load(); Assert.Equal(3, loaded.DecimalPlaces); Assert.True(loaded.AutoCalculate); Assert.Equal("Dark", loaded.Theme); Assert.Equal("ja-JP", loaded.Language); }
        finally { var folder = Path.GetDirectoryName(path)!; if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true); }
    }

    [Fact]
    public void ProjectMilestoneNotificationSettingPersistsAsOneApplicationValue()
    {
        var path = Path.Combine(Path.GetTempPath(), $"UrbanPlanToolbox-{Guid.NewGuid():N}", "settings.json");
        try
        {
            var service = new SettingsService(path);
            service.Save(new AppSettings { ProjectMilestoneNotificationsEnabled = true });
            var loaded = new SettingsService(path).Load();
            Assert.True(loaded.ProjectMilestoneNotificationsEnabled);
            Assert.True(loaded.IsProjectMilestoneNotificationsEnabled);
        }
        finally
        {
            var folder = Path.GetDirectoryName(path)!;
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void LegacySettingsHaveSafeDisabledDefaultUntilReminderMigrationRuns()
    {
        var settings = new AppSettings();
        Assert.Null(settings.ProjectMilestoneNotificationsEnabled);
        Assert.False(settings.IsProjectMilestoneNotificationsEnabled);
    }

    [Fact]
    public void ThemeAndLanguagePersistIndependently()
    {
        var path = Path.Combine(Path.GetTempPath(), $"UrbanPlanToolbox-{Guid.NewGuid():N}", "settings.json");
        try
        {
            var service = new SettingsService(path);
            service.Save(new AppSettings { Theme = "Dark", Language = "zh-CN" });
            service.Update(settings => settings.Language = "ja-JP");
            var loaded = service.Load();
            Assert.Equal("Dark", loaded.Theme);
            Assert.Equal("ja-JP", loaded.Language);
        }
        finally { var folder = Path.GetDirectoryName(path)!; if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true); }
    }

    [Fact]
    public void LegacyToolSpecificSettingsRemainReadableWithoutSettingsPageControls()
    {
        var path = Path.Combine(Path.GetTempPath(), $"UrbanPlanToolbox-{Guid.NewGuid():N}", "settings.json");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "{\"SchemaVersion\":1,\"Theme\":\"System\",\"DecimalPlaces\":3,\"AutoCalculate\":true,\"Language\":\"en-US\",\"FavoriteToolIds\":[]}");
            var loaded = new SettingsService(path).Load();
            Assert.Equal(3, loaded.DecimalPlaces);
            Assert.True(loaded.AutoCalculate);
            Assert.Equal("en-US", loaded.Language);
        }
        finally { var folder = Path.GetDirectoryName(path)!; if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true); }
    }
}
