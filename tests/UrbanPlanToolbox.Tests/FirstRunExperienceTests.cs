using UrbanPlanToolbox.Services;
using UrbanPlanToolbox.Models;
using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class FirstRunExperienceTests
{
    [Fact]
    public void NewInstallationShowsGuideAndCompletionPersists()
    {
        using var scope = new TemporaryState();
        var service = new FirstRunExperienceService(scope.Path, () => false);

        Assert.True(service.ShouldShowAutomatically());
        Assert.False(service.IsCompleted);
        Assert.True(service.TryMarkCompleted(out var error), error);
        Assert.False(service.ShouldShowAutomatically());
        Assert.True(service.IsCompleted);
    }

    [Fact]
    public void LegacyInstallationIsMigratedOnceAndDoesNotShowAutomatically()
    {
        using var scope = new TemporaryState();
        var service = new FirstRunExperienceService(scope.Path, () => true);

        Assert.False(service.ShouldShowAutomatically());
        Assert.True(File.Exists(scope.Path));

        var reopened = new FirstRunExperienceService(scope.Path, () => false);
        Assert.False(reopened.ShouldShowAutomatically());
    }

    [Fact]
    public void InvalidStateFallsBackToNewInstallation()
    {
        using var scope = new TemporaryState();
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(scope.Path)!);
        File.WriteAllText(scope.Path, "{ invalid json");
        var service = new FirstRunExperienceService(scope.Path, () => false);

        Assert.True(service.ShouldShowAutomatically());
    }

    [Fact]
    public void ManualReopenDoesNotResetCompletion()
    {
        using var scope = new TemporaryState();
        var service = new FirstRunExperienceService(scope.Path, () => false);
        Assert.True(service.TryMarkCompleted(out var error), error);

        // Manual opening is intentionally a host concern; the lifecycle service never exposes a reset operation.
        Assert.False(service.ShouldShowAutomatically());
    }

    [Fact]
    public void LifecycleStateDistinguishesNewInstallationAndCompletion()
    {
        using var scope = new TemporaryState();
        var service = new FirstRunExperienceService(scope.Path, () => false);

        Assert.Equal(FirstRunGuideInstallationState.NewInstallation, service.InstallationState);
        Assert.True(service.TryMarkCompleted(out var error), error);
        Assert.Equal(FirstRunGuideInstallationState.Completed, service.InstallationState);
    }

    [Fact]
    public void LegacyMigrationIsPersistedAndNotReevaluated()
    {
        using var scope = new TemporaryState();
        var calls = 0;
        var service = new FirstRunExperienceService(scope.Path, () =>
        {
            calls++;
            return true;
        });

        Assert.False(service.ShouldShowAutomatically());
        var reopened = new FirstRunExperienceService(scope.Path, () => false);

        Assert.Equal(1, calls);
        Assert.False(reopened.ShouldShowAutomatically());
    }

    [Fact]
    public void PendingStateWithMissingCompletionStillShowsGuide()
    {
        using var scope = new TemporaryState();
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(scope.Path)!);
        File.WriteAllText(scope.Path, "{\"StateSchemaVersion\":1,\"InstallationState\":3,\"CompletedFirstRunGuideVersion\":0,\"LegacyInstallationMigrationEvaluated\":true}");

        var service = new FirstRunExperienceService(scope.Path, () => true);

        Assert.True(service.ShouldShowAutomatically());
        Assert.False(service.IsCompleted);
    }

    [Fact]
    public void GuideResourcesHaveMatchingKeysAndNonEmptyValues()
    {
        var catalogs = ReswCatalog.Languages.Select(ReswCatalog.Load).ToArray();
        Assert.All(catalogs, catalog => Assert.All(catalog.Where(pair => pair.Key.StartsWith("FirstRunGuide_", StringComparison.Ordinal)), pair => Assert.False(string.IsNullOrWhiteSpace(pair.Value), pair.Key)));
        Assert.Equal(catalogs[0].Keys.OrderBy(x => x), catalogs[1].Keys.OrderBy(x => x));
        Assert.Equal(catalogs[0].Keys.OrderBy(x => x), catalogs[2].Keys.OrderBy(x => x));
        foreach (var key in new[] { "FirstRunGuide_Step", "FirstRunGuide_Step1Title", "FirstRunGuide_Step4Body", "FirstRunGuide_SettingsAction" })
            Assert.All(catalogs, catalog => Assert.True(catalog.ContainsKey(key), key));
    }

    private sealed class TemporaryState : IDisposable
    {
        private readonly string _directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "UrbanPlanToolbox-first-run-" + Guid.NewGuid().ToString("N"));
        public string Path => System.IO.Path.Combine(_directory, "first-run-guide.json");
        public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true); }
    }
}
