using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Services;
using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class BackgroundResidencySettingsTests
{
    [Fact]
    public void NewUsersDefaultBothBackgroundOptionsToOff()
    {
        var settings = SettingsService.CreateDefaults();
        Assert.False(settings.BackgroundResidencyEnabled);
        Assert.False(settings.SilentStartupShowRecorder);
    }

    [Fact]
    public void LegacyEnabledValuesMigrateToTheTwoNewOptions()
    {
        using var scope = new SettingsScope("{\"CloseToTrayEnabled\":true,\"InspirationRecorderEnabled\":true,\"StartWithWindows\":true,\"ShowRecorderOnBackgroundStartup\":true}");
        var settings = new SettingsService(scope.Path).Load();
        Assert.True(settings.BackgroundResidencyEnabled);
        Assert.True(settings.SilentStartupShowRecorder);
    }

    [Fact]
    public void SilentStartupAlwaysRequiresBackgroundResidency()
    {
        var settings = SettingsService.Normalize(new AppSettings { BackgroundResidencyEnabled = false, SilentStartupShowRecorder = true });
        Assert.True(settings.BackgroundResidencyEnabled);
    }

    private sealed class SettingsScope : IDisposable
    {
        private readonly string _directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        public string Path => System.IO.Path.Combine(_directory, "settings.json");
        public SettingsScope(string json) { Directory.CreateDirectory(_directory); File.WriteAllText(Path, json); }
        public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
    }
}
