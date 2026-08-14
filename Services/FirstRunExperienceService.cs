using System.Text.Json;
using UrbanPlanToolbox.Models;
using Windows.Storage;

namespace UrbanPlanToolbox.Services;

/// <summary>Owns package-scoped onboarding lifecycle state; user business data is never completion evidence.</summary>
public sealed class FirstRunExperienceService : IFirstRunExperienceService
{
    public const int CurrentVersion = 1;
    private const int CurrentSchemaVersion = 2;
    public static FirstRunExperienceService Default { get; } = new();
    private readonly string _statePath;
    private readonly object _gate = new();
    private FirstRunGuideState? _state;

    public FirstRunExperienceService(string? statePath = null) => _statePath = statePath ?? ResolvePackagedStatePath();

    public int CurrentFirstRunGuideVersion => CurrentVersion;
    public void PrepareForLaunch() { lock (_gate) _ = LoadOrMigrate(); }
    public FirstRunGuideInstallationState InstallationState { get { lock (_gate) return LoadOrMigrate().InstallationState; } }
    public bool IsCompleted { get { lock (_gate) return LoadOrMigrate().CompletedFirstRunGuideVersion >= CurrentVersion; } }

    public bool ShouldShowAutomatically()
    {
        lock (_gate)
        {
            var state = LoadOrMigrate();
            var shouldShow = state.CompletedFirstRunGuideVersion < CurrentVersion;
            Log("AutomaticDecision", state, $"ShouldShow={shouldShow}");
            return shouldShow;
        }
    }

    public bool TryMarkCompleted(out string? error)
    {
        lock (_gate)
        {
            var state = LoadOrMigrate();
            state.CompletedFirstRunGuideVersion = CurrentVersion;
            state.InstallationState = FirstRunGuideInstallationState.Completed;
            var saved = TrySave(state, out error);
            if (saved) Log("Completed", state);
            return saved;
        }
    }

    private FirstRunGuideState LoadOrMigrate()
    {
        if (_state is not null) return _state;
        var result = ReadState();
        var state = result.State;
        if (result.IsFutureSchema) return SetState(FailSafePending(), "StateLoaded", "FutureSchema=true;Preserved=true");
        if (result.IsMissing || result.IsInvalid)
        {
            state = NewPendingState();
            if (!TrySave(state, out _)) Log("StateSaveFailed", state);
            return SetState(state, result.IsMissing ? "StateMissing" : "StateLoaded", result.IsInvalid ? "Invalid=true" : null);
        }
        if (state.StateSchemaVersion == 1)
        {
            var synthetic = state.InstallationState == FirstRunGuideInstallationState.ExistingUserMigrated;
            state.StateSchemaVersion = CurrentSchemaVersion;
            state.CompletedFirstRunGuideVersion = synthetic ? 0 : Math.Min(state.CompletedFirstRunGuideVersion, CurrentVersion);
            state.InstallationState = state.CompletedFirstRunGuideVersion >= CurrentVersion
                ? FirstRunGuideInstallationState.Completed : FirstRunGuideInstallationState.Pending;
            if (synthetic) Log("LegacySyntheticCompletionInvalidated", state);
            if (!TrySave(state, out _)) Log("StateSaveFailed", state);
            return SetState(state, "StateMigratedV1ToV2", $"SyntheticInvalidated={synthetic}");
        }
        state.CompletedFirstRunGuideVersion = Math.Min(state.CompletedFirstRunGuideVersion, CurrentVersion);
        state.InstallationState = state.CompletedFirstRunGuideVersion >= CurrentVersion ? FirstRunGuideInstallationState.Completed : FirstRunGuideInstallationState.Pending;
        return SetState(state, "StateLoaded");
    }

    private FirstRunGuideState SetState(FirstRunGuideState state, string eventName, string? extra = null)
    {
        _state = state;
        Log(eventName, state, extra);
        return state;
    }

    private static FirstRunGuideState NewPendingState() => new() { StateSchemaVersion = CurrentSchemaVersion, InstallationState = FirstRunGuideInstallationState.NewInstallation, CompletedFirstRunGuideVersion = 0 };
    private static FirstRunGuideState FailSafePending() => new() { StateSchemaVersion = CurrentSchemaVersion, InstallationState = FirstRunGuideInstallationState.Pending, CompletedFirstRunGuideVersion = 0 };

    private (FirstRunGuideState State, bool IsMissing, bool IsInvalid, bool IsFutureSchema) ReadState()
    {
        try
        {
            if (!File.Exists(_statePath)) return (NewPendingState(), true, false, false);
            var state = JsonSerializer.Deserialize<FirstRunGuideState>(File.ReadAllText(_statePath));
            if (state is null || state.CompletedFirstRunGuideVersion < 0 || !Enum.IsDefined(state.InstallationState)) return (FailSafePending(), false, true, false);
            if (state.StateSchemaVersion > CurrentSchemaVersion) return (FailSafePending(), false, false, true);
            if (state.StateSchemaVersion is not 1 and not 2) return (FailSafePending(), false, true, false);
            return (state, false, false, false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return (FailSafePending(), false, true, false);
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
            try { File.WriteAllText(temporary, JsonSerializer.Serialize(state)); File.Move(temporary, _statePath, true); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            _state = state;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = ex.Message;
            Log("StateSaveFailed", state);
            return false;
        }
    }

    private static void Log(string eventName, FirstRunGuideState state, string? extra = null) =>
        AppLogger.Default.Info("FirstRun", eventName, $"Schema={state.StateSchemaVersion};CompletedVersion={state.CompletedFirstRunGuideVersion};CurrentVersion={CurrentVersion};State={state.InstallationState}{(string.IsNullOrWhiteSpace(extra) ? string.Empty : ";" + extra)}");

    private static string ResolvePackagedStatePath()
    {
        try { return Path.Combine(ApplicationData.Current.LocalFolder.Path, "first-run-guide.json"); }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException) { return Path.Combine(AppDataPathProvider.Default.Paths.RootDirectory, "first-run-guide.json"); }
    }
}
