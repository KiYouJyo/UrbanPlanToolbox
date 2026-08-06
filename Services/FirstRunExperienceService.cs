using System.Text.Json;
using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.Services;

/// <summary>
/// Owns first-run lifecycle state separately from user preferences and backup data.
/// </summary>
public sealed class FirstRunExperienceService : IFirstRunExperienceService
{
    public const int CurrentVersion = 1;
    private readonly string _statePath;
    private readonly Func<bool> _legacyInstallationExists;
    private readonly object _gate = new();
    private FirstRunGuideState? _state;

    public FirstRunExperienceService(string? statePath = null, Func<bool>? legacyInstallationExists = null)
    {
        _statePath = statePath ?? Path.Combine(AppDataPathProvider.Default.Paths.RootDirectory, "first-run-guide.json");
        _legacyInstallationExists = legacyInstallationExists ?? DetectLegacyInstallation;
    }

    public int CurrentFirstRunGuideVersion => CurrentVersion;

    public bool IsCompleted
    {
        get
        {
            lock (_gate) return LoadOrMigrate().CompletedFirstRunGuideVersion >= CurrentVersion;
        }
    }

    public bool ShouldShowAutomatically()
    {
        lock (_gate)
        {
            var state = LoadOrMigrate();
            return state.CompletedFirstRunGuideVersion < CurrentVersion;
        }
    }

    public bool TryMarkCompleted(out string? error)
    {
        lock (_gate)
        {
            var state = LoadOrMigrate();
            state.CompletedFirstRunGuideVersion = CurrentVersion;
            return TrySave(state, out error);
        }
    }

    private FirstRunGuideState LoadOrMigrate()
    {
        if (_state is not null) return _state;

        var state = ReadState();
        if (!state.LegacyInstallationMigrationEvaluated)
        {
            state.LegacyInstallationMigrationEvaluated = true;
            if (_legacyInstallationExists()) state.CompletedFirstRunGuideVersion = CurrentVersion;
            // A failed write is deliberately non-fatal. The next launch retries this one-time decision.
            TrySave(state, out _);
        }

        _state = state;
        return state;
    }

    private FirstRunGuideState ReadState()
    {
        try
        {
            if (!File.Exists(_statePath)) return new FirstRunGuideState();
            var state = JsonSerializer.Deserialize<FirstRunGuideState>(File.ReadAllText(_statePath)) ?? new FirstRunGuideState();
            if (state.StateSchemaVersion != 1 || state.CompletedFirstRunGuideVersion < 0) return new FirstRunGuideState();
            state.CompletedFirstRunGuideVersion = Math.Min(state.CompletedFirstRunGuideVersion, CurrentVersion);
            return state;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new FirstRunGuideState();
        }
    }

    private bool TrySave(FirstRunGuideState state, out string? error)
    {
        error = null;
        try
        {
            var directory = Path.GetDirectoryName(_statePath);
            if (string.IsNullOrWhiteSpace(directory)) throw new IOException("The first-run state directory is unavailable.");
            Directory.CreateDirectory(directory);
            var temporary = $"{_statePath}.{Guid.NewGuid():N}.tmp";
            try
            {
                File.WriteAllText(temporary, JsonSerializer.Serialize(state));
                File.Move(temporary, _statePath, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }

            _state = state;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = ex.Message;
            return false;
        }
    }

    private bool DetectLegacyInstallation()
    {
        var paths = AppDataPathProvider.Default.Paths;
        return File.Exists(paths.SettingsFilePath) || ContainsFiles(paths.DataDirectory) || ContainsFiles(paths.AttachmentsDirectory);
    }

    private static bool ContainsFiles(string directory) => Directory.Exists(directory) && Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Any();
}
