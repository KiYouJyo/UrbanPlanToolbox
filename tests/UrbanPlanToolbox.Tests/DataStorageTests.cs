using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Models.Tools;
using UrbanPlanToolbox.Services;
using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class DataStorageTests
{
    [Fact]
    public void PathProviderBuildsStableIsolatedDirectoriesWithoutCreatingToolDataEarly()
    {
        using var scope = new TemporaryDataScope();
        var paths = scope.Provider.Paths;

        Assert.Equal(Path.Combine(scope.Root, "data"), paths.DataDirectory);
        Assert.Equal(Path.Combine(scope.Root, "data", "tools"), paths.ToolsDirectory);
        Assert.Equal(Path.Combine(scope.Root, "attachments"), paths.AttachmentsDirectory);
        Assert.Equal(Path.Combine(scope.Root, "backups"), paths.BackupsDirectory);
        Assert.Equal(Path.Combine(scope.Root, "cache"), paths.CacheDirectory);
        Assert.Equal(Path.Combine(scope.Root, "logs"), paths.LogsDirectory);
        Assert.False(Directory.Exists(Path.Combine(paths.ToolsDirectory, ToolIds.PlanningIndicatorCalculator)));

        scope.Provider.EnsureInfrastructureDirectories();

        Assert.True(Directory.Exists(paths.ToolsDirectory));
        Assert.False(Directory.Exists(Path.Combine(paths.ToolsDirectory, ToolIds.PlanningIndicatorCalculator)));
    }

    [Fact]
    public void ToolDirectoriesUseStableIdsAndRemainIndependent()
    {
        using var scope = new TemporaryDataScope();
        var first = scope.Provider.GetToolDataDirectory(ToolIds.PlanningIndicatorCalculator);
        var second = scope.Provider.GetToolDataDirectory(ToolIds.UnitScaleConverter);

        Assert.EndsWith(ToolIds.PlanningIndicatorCalculator, first, StringComparison.Ordinal);
        Assert.EndsWith(ToolIds.UnitScaleConverter, second, StringComparison.Ordinal);
        Assert.NotEqual(first, second);
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("..")]
    [InlineData("C:\\escape")]
    [InlineData("not-registered")]
    public void InvalidOrUnregisteredToolIdsAreRejected(string toolId)
    {
        using var scope = new TemporaryDataScope();
        Assert.Throws<ArgumentException>(() => scope.Provider.GetToolDataDirectory(toolId));
    }

    [Theory]
    [InlineData("../data.json")]
    [InlineData("nested/data.json")]
    [InlineData("C:\\data.json")]
    [InlineData("data.txt")]
    public void UnsafeDataFileNamesAreRejected(string fileName)
    {
        using var scope = new TemporaryDataScope();
        Assert.Throws<ArgumentException>(() => scope.Provider.GetToolDataFilePath(ToolIds.UnitScaleConverter, fileName));
    }

    [Fact]
    public async Task MissingFileReturnsNotFoundWithoutCreatingBusinessData()
    {
        using var scope = new TemporaryDataScope();
        var result = await scope.Storage.ReadAsync<TestPayload>(ToolIds.UnitScaleConverter, "records.json");

        Assert.Equal(DataStorageStatus.NotFound, result.Status);
        Assert.Empty(Directory.GetFiles(scope.Provider.GetToolDataDirectory(ToolIds.UnitScaleConverter)));
    }

    [Fact]
    public async Task SaveAndReadUseStableEnvelopeAndUtf8()
    {
        using var scope = new TemporaryDataScope();
        var payload = new TestPayload("规划・計画", 7);

        Assert.True((await scope.Storage.SaveAsync(ToolIds.UnitScaleConverter, "records.json", payload)).Succeeded);
        var loaded = await scope.Storage.ReadAsync<TestPayload>(ToolIds.UnitScaleConverter, "records.json");
        var bytes = await File.ReadAllBytesAsync(scope.DataPath("records.json"));
        var json = Encoding.UTF8.GetString(bytes);

        Assert.Equal(DataStorageStatus.Success, loaded.Status);
        Assert.Equal(payload, loaded.Value);
        Assert.StartsWith("{", json);
        Assert.Contains("\"schemaVersion\": 1", json);
        Assert.Contains("\"savedAtUtc\"", json);
        Assert.Contains("\"payload\"", json);
        Assert.DoesNotContain("SchemaVersion", json);
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
    }

    [Fact]
    public async Task ValueTypePayloadRoundTripsWithoutFallingBackToDefault()
    {
        using var scope = new TemporaryDataScope();

        await scope.Storage.SaveAsync(ToolIds.UnitScaleConverter, "number.json", 42);
        var loaded = await scope.Storage.ReadAsync<int>(ToolIds.UnitScaleConverter, "number.json");

        Assert.Equal(DataStorageStatus.Success, loaded.Status);
        Assert.Equal(42, loaded.Value);
    }

    [Fact]
    public async Task MultipleSavesReturnLatestValidPayloadAndKeepOneBackup()
    {
        using var scope = new TemporaryDataScope();
        await scope.Storage.SaveAsync(ToolIds.UnitScaleConverter, "records.json", new TestPayload("one", 1));
        await scope.Storage.SaveAsync(ToolIds.UnitScaleConverter, "records.json", new TestPayload("two", 2));
        await scope.Storage.SaveAsync(ToolIds.UnitScaleConverter, "records.json", new TestPayload("three", 3));

        var loaded = await scope.Storage.ReadAsync<TestPayload>(ToolIds.UnitScaleConverter, "records.json");
        var backups = Directory.GetFiles(scope.Provider.GetToolBackupDirectory(ToolIds.UnitScaleConverter), "*.last-valid.bak");

        Assert.Equal(new TestPayload("three", 3), loaded.Value);
        Assert.Single(backups);
        Assert.DoesNotContain(Directory.GetFiles(Path.GetDirectoryName(scope.DataPath("records.json"))!), path => path.EndsWith(".tmp", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SerializationFailureDoesNotReplaceExistingFile()
    {
        using var scope = new TemporaryDataScope();
        var original = new TestPayload("valid", 1);
        await scope.Storage.SaveAsync(ToolIds.UnitScaleConverter, "records.json", original);
        var before = await File.ReadAllBytesAsync(scope.DataPath("records.json"));
        var cycle = new CyclicPayload();
        cycle.Self = cycle;

        var failed = await scope.Storage.SaveAsync(ToolIds.UnitScaleConverter, "records.json", cycle);
        var after = await File.ReadAllBytesAsync(scope.DataPath("records.json"));

        Assert.Equal(DataStorageStatus.IoFailure, failed.Status);
        Assert.Equal(before, after);
        Assert.Equal(original, (await scope.Storage.ReadAsync<TestPayload>(ToolIds.UnitScaleConverter, "records.json")).Value);
    }

    [Fact]
    public async Task CorruptPrimaryRecoversFromLastValidBackupAndPreservesDiagnosticCopy()
    {
        using var scope = new TemporaryDataScope();
        var first = new TestPayload("first", 1);
        await scope.Storage.SaveAsync(ToolIds.UnitScaleConverter, "records.json", first);
        await scope.Storage.SaveAsync(ToolIds.UnitScaleConverter, "records.json", new TestPayload("second", 2));
        await File.WriteAllTextAsync(scope.DataPath("records.json"), "{broken", Encoding.UTF8);

        var recovered = await scope.Storage.ReadAsync<TestPayload>(ToolIds.UnitScaleConverter, "records.json");

        Assert.Equal(DataStorageStatus.RecoveredFromBackup, recovered.Status);
        Assert.Equal(first, recovered.Value);
        Assert.Single(Directory.GetFiles(scope.Provider.GetToolBackupDirectory(ToolIds.UnitScaleConverter), "*.corrupt-*"));
        Assert.Equal(first, (await scope.Storage.ReadAsync<TestPayload>(ToolIds.UnitScaleConverter, "records.json")).Value);
    }

    [Fact]
    public async Task CorruptPrimaryAndBackupReturnCorruptWithoutDeletion()
    {
        using var scope = new TemporaryDataScope();
        await scope.Storage.SaveAsync(ToolIds.UnitScaleConverter, "records.json", new TestPayload("first", 1));
        await scope.Storage.SaveAsync(ToolIds.UnitScaleConverter, "records.json", new TestPayload("second", 2));
        var dataPath = scope.DataPath("records.json");
        var backupPath = scope.Provider.GetToolBackupFilePath(ToolIds.UnitScaleConverter, "records.json");
        await File.WriteAllTextAsync(dataPath, "bad-primary", Encoding.UTF8);
        await File.WriteAllTextAsync(backupPath, "bad-backup", Encoding.UTF8);

        var result = await scope.Storage.ReadAsync<TestPayload>(ToolIds.UnitScaleConverter, "records.json");

        Assert.Equal(DataStorageStatus.Corrupt, result.Status);
        Assert.True(File.Exists(dataPath));
        Assert.True(File.Exists(backupPath));
    }

    [Fact]
    public async Task FutureVersionIsRejectedAndNeverOverwritten()
    {
        using var scope = new TemporaryDataScope();
        scope.WriteEnvelope("records.json", 2, new TestPayload("future", 2));
        var before = await File.ReadAllBytesAsync(scope.DataPath("records.json"));

        var read = await scope.Storage.ReadAsync<TestPayload>(ToolIds.UnitScaleConverter, "records.json");
        var write = await scope.Storage.SaveAsync(ToolIds.UnitScaleConverter, "records.json", new TestPayload("old-app", 1));

        Assert.Equal(DataStorageStatus.UnsupportedFutureVersion, read.Status);
        Assert.Equal(DataStorageStatus.UnsupportedFutureVersion, write.Status);
        Assert.Equal(before, await File.ReadAllBytesAsync(scope.DataPath("records.json")));
    }

    [Fact]
    public async Task SingleAndMultiStepMigrationsRunInOrderOnlyOnce()
    {
        using var scope = new TemporaryDataScope(3,
        [
            new PropertyMigration("v1-v2", 1, "step2", "done"),
            new PropertyMigration("v2-v3", 2, "step3", "done")
        ]);
        scope.WriteEnvelope("records.json", 1, new { name = "old", number = 1 });

        var first = await scope.Storage.ReadAsync<MigratedPayload>(ToolIds.UnitScaleConverter, "records.json");
        var second = await scope.Storage.ReadAsync<MigratedPayload>(ToolIds.UnitScaleConverter, "records.json");

        Assert.Equal(DataStorageStatus.Success, first.Status);
        Assert.Equal(3, first.SchemaVersion);
        Assert.Equal(new MigratedPayload("old", 1, "done", "done"), first.Value);
        Assert.Equal(first.Value, second.Value);
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(scope.DataPath("records.json")));
        Assert.Equal(3, document.RootElement.GetProperty("schemaVersion").GetInt32());
    }

    [Fact]
    public async Task MissingMigrationFailsWithoutChangingOriginal()
    {
        using var scope = new TemporaryDataScope(3, [new PropertyMigration("v1-v2", 1, "step2", "done")]);
        scope.WriteEnvelope("records.json", 1, new { name = "old", number = 1 });
        var before = await File.ReadAllBytesAsync(scope.DataPath("records.json"));

        var result = await scope.Storage.ReadAsync<MigratedPayload>(ToolIds.UnitScaleConverter, "records.json");

        Assert.Equal(DataStorageStatus.MigrationFailed, result.Status);
        Assert.Equal(before, await File.ReadAllBytesAsync(scope.DataPath("records.json")));
    }

    [Fact]
    public async Task FailedMigrationDoesNotUpdateVersionOrPayload()
    {
        using var scope = new TemporaryDataScope(3,
        [
            new PropertyMigration("v1-v2", 1, "step2", "done"),
            new ThrowingMigration(2)
        ]);
        scope.WriteEnvelope("records.json", 1, new { name = "old", number = 1 });
        var before = await File.ReadAllBytesAsync(scope.DataPath("records.json"));

        var result = await scope.Storage.ReadAsync<MigratedPayload>(ToolIds.UnitScaleConverter, "records.json");

        Assert.Equal(DataStorageStatus.MigrationFailed, result.Status);
        Assert.Equal(before, await File.ReadAllBytesAsync(scope.DataPath("records.json")));
    }

    [Fact]
    public async Task InvalidMigratedPayloadIsNotWritten()
    {
        using var scope = new TemporaryDataScope(2, [new PropertyMigration("v1-v2", 1, "unrelated", "value")]);
        scope.WriteEnvelope("records.json", 1, new { name = "old" });
        var before = await File.ReadAllBytesAsync(scope.DataPath("records.json"));

        var result = await scope.Storage.ReadAsync<StrictMigratedPayload>(ToolIds.UnitScaleConverter, "records.json");

        Assert.Equal(DataStorageStatus.MigrationFailed, result.Status);
        Assert.Equal(before, await File.ReadAllBytesAsync(scope.DataPath("records.json")));
    }

    [Fact]
    public async Task OlderValidBackupIsMigratedBeforeRecovery()
    {
        using var scope = new TemporaryDataScope(2, [new PropertyMigration("v1-v2", 1, "step2", "done")]);
        scope.WriteEnvelope("records.json", 1, new { name = "old" });
        File.Copy(
            scope.DataPath("records.json"),
            scope.Provider.GetToolBackupFilePath(ToolIds.UnitScaleConverter, "records.json"));
        await File.WriteAllTextAsync(scope.DataPath("records.json"), "broken", Encoding.UTF8);

        var result = await scope.Storage.ReadAsync<MigratedV2Payload>(ToolIds.UnitScaleConverter, "records.json");

        Assert.Equal(DataStorageStatus.RecoveredFromBackup, result.Status);
        Assert.Equal(2, result.SchemaVersion);
        Assert.Equal(new MigratedV2Payload("old", "done"), result.Value);
    }

    [Fact]
    public void DuplicateAndNonSequentialMigrationsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => new DataMigrationRunner(
        [
            new PropertyMigration("first", 1, "a", "a"),
            new PropertyMigration("duplicate", 1, "b", "b")
        ]));
        Assert.Throws<ArgumentException>(() => new DataMigrationRunner([new InvalidJumpMigration()]));
    }

    [Fact]
    public async Task ConcurrentWritesNeverProducePartialJson()
    {
        using var scope = new TemporaryDataScope();
        var writes = Enumerable.Range(0, 20)
            .Select(index => scope.Storage.SaveAsync(ToolIds.UnitScaleConverter, "records.json", new TestPayload($"value-{index}", index)));

        var results = await Task.WhenAll(writes);
        var loaded = await scope.Storage.ReadAsync<TestPayload>(ToolIds.UnitScaleConverter, "records.json");

        Assert.All(results, result => Assert.True(result.Succeeded));
        Assert.True(loaded.HasValue);
        Assert.InRange(loaded.Value!.Number, 0, 19);
        _ = JsonDocument.Parse(await File.ReadAllTextAsync(scope.DataPath("records.json")));
    }

    [Fact]
    public void ExistingSettingsFormatRoundTripsAtItsUnchangedLocation()
    {
        using var scope = new TemporaryDataScope();
        var settings = new AppSettings
        {
            Theme = "Dark",
            DecimalPlaces = 4,
            AutoCalculate = true,
            Language = "ja-JP",
            FavoriteToolIds = [ToolIds.UnitScaleConverter]
        };
        var service = new SettingsService(scope.Provider.Paths.SettingsFilePath);

        service.Save(settings);
        var restored = service.Load();

        Assert.Equal(settings.Theme, restored.Theme);
        Assert.Equal(settings.DecimalPlaces, restored.DecimalPlaces);
        Assert.Equal(settings.AutoCalculate, restored.AutoCalculate);
        Assert.Equal(settings.Language, restored.Language);
        Assert.Equal(settings.FavoriteToolIds, restored.FavoriteToolIds);
        Assert.Equal(Path.Combine(scope.Root, "settings.json"), scope.Provider.Paths.SettingsFilePath);
    }

    private sealed record TestPayload(string Name, int Number);
    private sealed record MigratedPayload(string Name, int Number, string Step2, string Step3);
    private sealed record MigratedV2Payload(string Name, string Step2);
    private sealed class StrictMigratedPayload
    {
        public required string Name { get; init; }
        public required string RequiredAfterMigration { get; init; }
    }
    private sealed class CyclicPayload { public CyclicPayload? Self { get; set; } }

    private sealed class PropertyMigration(string name, int fromVersion, string propertyName, string value) : IDataMigration
    {
        public string Name => name;
        public int FromVersion => fromVersion;
        public int ToVersion => fromVersion + 1;
        public JsonNode Apply(JsonNode payload)
        {
            payload.AsObject()[propertyName] = value;
            return payload;
        }
    }

    private sealed class ThrowingMigration(int fromVersion) : IDataMigration
    {
        public string Name => "throws";
        public int FromVersion => fromVersion;
        public int ToVersion => fromVersion + 1;
        public JsonNode Apply(JsonNode payload) => throw new InvalidOperationException("Expected test failure.");
    }

    private sealed class InvalidJumpMigration : IDataMigration
    {
        public string Name => "jump";
        public int FromVersion => 1;
        public int ToVersion => 3;
        public JsonNode Apply(JsonNode payload) => payload;
    }

    private sealed class TemporaryDataScope : IDisposable
    {
        public TemporaryDataScope(int schemaVersion = 1, IEnumerable<IDataMigration>? migrations = null)
        {
            Root = Path.Combine(Path.GetTempPath(), $"UrbanPlanToolbox-storage-{Guid.NewGuid():N}");
            Provider = new AppDataPathProvider(Root, [ToolIds.PlanningIndicatorCalculator, ToolIds.UnitScaleConverter]);
            Storage = new JsonDataStorage(Provider, schemaVersion, migrations);
        }

        public string Root { get; }
        public AppDataPathProvider Provider { get; }
        public JsonDataStorage Storage { get; }

        public string DataPath(string fileName) => Provider.GetToolDataFilePath(ToolIds.UnitScaleConverter, fileName);

        public void WriteEnvelope<T>(string fileName, int schemaVersion, T payload)
        {
            var envelope = new DataEnvelope<T>
            {
                SchemaVersion = schemaVersion,
                SavedAtUtc = DateTimeOffset.UtcNow,
                Payload = payload
            };
            File.WriteAllText(DataPath(fileName), JsonSerializer.Serialize(envelope, DataStorageJson.Options), new UTF8Encoding(false));
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
