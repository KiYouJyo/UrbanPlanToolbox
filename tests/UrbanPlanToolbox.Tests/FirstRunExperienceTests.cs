using System.Text.Json;
using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Services;
using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class FirstRunExperienceTests
{
    [Fact] public void FreshInstallationShowsGuide() { using var s = new TemporaryState(); Assert.True(new FirstRunExperienceService(s.Path).ShouldShowAutomatically()); }

    [Fact]
    public void CompletedInstallationDoesNotShowGuide()
    {
        using var s = new TemporaryState(); var service = new FirstRunExperienceService(s.Path);
        Assert.True(service.TryMarkCompleted(out var error), error); Assert.False(service.ShouldShowAutomatically());
        Assert.False(new FirstRunExperienceService(s.Path).ShouldShowAutomatically());
    }

    [Theory]
    [InlineData("settings.json")]
    [InlineData("data/projects/project.json")]
    [InlineData("attachments/photo.bin")]
    public void ResetOrReinstallWithRetainedBusinessDataShowsGuide(string retainedFile)
    {
        using var s = new TemporaryState(); var first = new FirstRunExperienceService(s.Path);
        Assert.True(first.TryMarkCompleted(out var error), error);
        var retained = Path.Combine(s.Directory, retainedFile); Directory.CreateDirectory(Path.GetDirectoryName(retained)!); File.WriteAllText(retained, "data");
        File.Delete(s.Path); // Package LocalState removed by Reset/uninstall; business data intentionally remains.
        var reinstalled = new FirstRunExperienceService(s.Path);
        Assert.False(reinstalled.IsCompleted); Assert.True(reinstalled.ShouldShowAutomatically());
    }

    [Fact]
    public void ExternalDataNeverCompletesOnboarding()
    {
        using var s = new TemporaryState();
        foreach (var relative in new[] { "settings.json", "data/projects/a.json", "attachments/a.bin" }) { var p = Path.Combine(s.Directory, relative); Directory.CreateDirectory(Path.GetDirectoryName(p)!); File.WriteAllText(p, "data"); }
        var service = new FirstRunExperienceService(s.Path);
        Assert.False(service.IsCompleted); Assert.True(service.ShouldShowAutomatically());
    }

    [Fact]
    public void V1SyntheticLegacyCompletionIsInvalidated()
    {
        using var s = new TemporaryState(); WriteState(s.Path, 1, FirstRunGuideInstallationState.ExistingUserMigrated, 1, true);
        var service = new FirstRunExperienceService(s.Path);
        Assert.Equal(FirstRunGuideInstallationState.Pending, service.InstallationState); Assert.False(service.IsCompleted); Assert.True(service.ShouldShowAutomatically());
        var migrated = JsonSerializer.Deserialize<FirstRunGuideState>(File.ReadAllText(s.Path))!;
        Assert.Equal(2, migrated.StateSchemaVersion); Assert.Equal(0, migrated.CompletedFirstRunGuideVersion);
    }

    [Fact]
    public void V1RealCompletedStateIsPreserved()
    {
        using var s = new TemporaryState(); WriteState(s.Path, 1, FirstRunGuideInstallationState.Completed, 1, true);
        var service = new FirstRunExperienceService(s.Path);
        Assert.Equal(FirstRunGuideInstallationState.Completed, service.InstallationState); Assert.False(service.ShouldShowAutomatically());
        Assert.Equal(2, JsonSerializer.Deserialize<FirstRunGuideState>(File.ReadAllText(s.Path))!.StateSchemaVersion);
    }

    [Fact] public void CorruptStateShowsGuide() { using var s = new TemporaryState(); Directory.CreateDirectory(s.Directory); File.WriteAllText(s.Path, "{ invalid json"); Assert.True(new FirstRunExperienceService(s.Path).ShouldShowAutomatically()); }

    [Fact]
    public void UnsupportedFutureSchemaFailsSafeWithoutOverwritingState()
    {
        using var s = new TemporaryState(); WriteState(s.Path, 999, FirstRunGuideInstallationState.Completed, 1, false);
        Assert.True(new FirstRunExperienceService(s.Path).ShouldShowAutomatically());
        Assert.Equal(999, JsonDocument.Parse(File.ReadAllText(s.Path)).RootElement.GetProperty("StateSchemaVersion").GetInt32());
    }

    [Fact]
    public void ManualOpenBypassesAutomaticCompletionGate()
    {
        var root = FindRepositoryRoot(); var window = File.ReadAllText(Path.Combine(root, "MainWindow.xaml.cs"));
        Assert.Contains("ShowFirstRunGuideFromSettings() => ShowFirstRunGuide(FirstRunGuideLaunchMode.Manual)", window);
    }

    [Fact]
    public void GuideResourcesHaveMatchingKeysAndNonEmptyValues()
    {
        var catalogs = ReswCatalog.Languages.Select(ReswCatalog.Load).ToArray();
        Assert.All(catalogs, catalog => Assert.All(catalog.Where(pair => pair.Key.StartsWith("FirstRunGuide_", StringComparison.Ordinal)), pair => Assert.False(string.IsNullOrWhiteSpace(pair.Value), pair.Key)));
    }

    private static void WriteState(string path, int schema, FirstRunGuideInstallationState state, int completed, bool legacy) { Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, $"{{\"StateSchemaVersion\":{schema},\"InstallationState\":{(int)state},\"CompletedFirstRunGuideVersion\":{completed},\"LegacyInstallationMigrationEvaluated\":{legacy.ToString().ToLowerInvariant()}}}"); }
    private static string FindRepositoryRoot() { for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent) if (File.Exists(Path.Combine(d.FullName, "UrbanPlanToolbox.slnx"))) return d.FullName; throw new DirectoryNotFoundException(); }
    private sealed class TemporaryState : IDisposable { public string Directory { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "UrbanPlanToolbox-first-run-" + Guid.NewGuid().ToString("N")); public string Path => System.IO.Path.Combine(Directory, "first-run-guide.json"); public void Dispose() { if (System.IO.Directory.Exists(Directory)) System.IO.Directory.Delete(Directory, true); } }
}
