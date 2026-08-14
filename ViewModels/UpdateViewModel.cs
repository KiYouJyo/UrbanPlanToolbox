using System.ComponentModel;
using System.Runtime.CompilerServices;
using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Services;

namespace UrbanPlanToolbox.ViewModels;

public sealed class UpdateViewModel(IAppUpdateService service, IApplicationRestartService? restartService = null, IApplicationRestartRegistrationService? restartRegistrationService = null) : INotifyPropertyChanged
{
    private static UpdateViewModel? _defaultSession;
    private readonly IAppUpdateService _service = service;
    private readonly IApplicationRestartService _restartService = restartService ?? new NoOpApplicationRestartService();
    private readonly IApplicationRestartRegistrationService _restartRegistrationService = restartRegistrationService ?? new NoOpApplicationRestartRegistrationService();
    private readonly CancellationTokenSource _sessionLifetime = new();
    private readonly Dictionary<(string Version, string Locale), LocalizedReleaseNotes> _localizedNotes = new();
    private int _busy;
    private AppUpdateInfo _info = new(AppUpdateState.NotChecked);
    public event PropertyChangedEventHandler? PropertyChanged;
    /// <summary>One update-operation owner for the application process. Pages only attach to this session.</summary>
    public static UpdateViewModel GetOrCreateDefault(Func<UpdateViewModel> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        return LazyInitializer.EnsureInitialized(ref _defaultSession, factory)!;
    }
    public AppUpdateInfo Info { get => _info; private set { _info = value; OnChanged(); OnChanged(nameof(CanCheck)); OnChanged(nameof(CanInstall)); } }
    public double? Progress { get; private set; }
    public string? RestartFailureReason { get; private set; }
    public bool CanCheck => Volatile.Read(ref _busy) == 0;
    public bool CanInstall => CanCheck && (Info.IsUpdateAvailable || Info.NeedsFinalRestart);
    public string CurrentVersion => AppVersionProvider.DisplayVersion;
    public bool ShouldShowUpdateDialog => Info.IsUpdateAvailable;
    public bool HasChecked { get; private set; }
    public async Task SetLocalizedNotesAsync(IReleaseNotesProvider provider, string locale, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(Info.AvailableVersion)) return;
        var version = Info.AvailableVersion;
        var normalizedLocale = LocalizedReleaseNotesService.NormalizeLocale(locale);
        if (_localizedNotes.TryGetValue((version, normalizedLocale), out var cached))
        {
            Info = Info with { LocalizedReleaseNotes = cached, ReleaseNotes = null };
            return;
        }
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_sessionLifetime.Token, cancellationToken);
        var notes = await provider.GetAsync(version, normalizedLocale, linked.Token);
        if (notes is not null) _localizedNotes[(version, normalizedLocale)] = notes;
        if (notes is not null) Info = Info with { LocalizedReleaseNotes = notes, ReleaseNotes = null };
    }

    public async Task CheckAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _busy, 1) != 0) return;
        try
        {
            HasChecked = true;
            OnChanged(nameof(HasChecked));
            Progress = null;
            OnChanged(nameof(Progress));
            Info = Info with { State = AppUpdateState.Checking, Detail = null, ErrorCode = null };
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(_sessionLifetime.Token, cancellationToken);
            var result = await _service.CheckForUpdatesAsync(linked.Token);
            Info = result with
            {
                // A re-check must not make an already populated card flash empty while
                // the matching localized document is fetched again.
                LocalizedReleaseNotes = result.LocalizedReleaseNotes ?? Info.LocalizedReleaseNotes
            };
        }
        catch (OperationCanceledException) { Progress = null; OnChanged(nameof(Progress)); Info = Info with { State = AppUpdateState.Cancelled, Detail = "Cancelled" }; }
        finally { Interlocked.Exchange(ref _busy, 0); OnChanged(nameof(CanCheck)); OnChanged(nameof(CanInstall)); }
    }

    public async Task DownloadAndInstallAsync(CancellationToken cancellationToken = default)
    {
        if (Info.IsRestartRequired)
        {
            await RestartAndUpdateAsync(cancellationToken);
            return;
        }
        if (!CanInstall || Interlocked.Exchange(ref _busy, 1) != 0) return;
        if (Info.Source == UpdateInstallSource.Store)
        {
            await DownloadInstallAndRestartStoreAsync(cancellationToken);
            return;
        }
        var progressGate = new object();
        var acceptingProgress = true;
        try
        {
            RestartFailureReason = null;
            var progress = new Progress<AppUpdateProgress>(value =>
            {
                lock (progressGate)
                {
                    if (!acceptingProgress || !CanApplyProgress(Info.State, value.State)) return;

                    var normalized = AppUpdateProgress.NormalizeValue(value.Value);
                    if (value.State == AppUpdateState.Downloading)
                    {
                        if (normalized is double) Progress = normalized;
                    }
                    else if (value.State is AppUpdateState.Verifying or AppUpdateState.Installing or AppUpdateState.Completed or AppUpdateState.Failed or AppUpdateState.Cancelled)
                    {
                        Progress = value.State == AppUpdateState.Completed ? 1d : null;
                    }

                    Info = Info with { State = value.State, Detail = value.Detail };
                    OnChanged(nameof(Progress));
                }
            });
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(_sessionLifetime.Token, cancellationToken);
            var result = Info.IsReadyToInstall
                ? await _service.InstallPendingAsync(progress, linked.Token)
                : await _service.DownloadAndInstallAsync(progress, linked.Token);
            var finalState = result.State;
            // The awaited service result is authoritative; deployment completion may still require a user-initiated restart.
            lock (progressGate)
            {
                acceptingProgress = false;
                Progress = null;
                OnChanged(nameof(Progress));
                Info = Info with { State = finalState, Detail = result.Detail, ErrorCode = result.ErrorCode };
            }
        }
        catch (OperationCanceledException)
        {
            lock (progressGate)
            {
                acceptingProgress = false;
                Progress = null;
                OnChanged(nameof(Progress));
                Info = Info with { State = AppUpdateState.Cancelled, Detail = "Cancelled" };
            }
        }
        finally { Interlocked.Exchange(ref _busy, 0); OnChanged(nameof(CanCheck)); OnChanged(nameof(CanInstall)); }
    }

    private async Task DownloadInstallAndRestartStoreAsync(CancellationToken cancellationToken)
    {
        var restartRegistered = false;
        try
        {
            AppLogger.Default.Info("StoreUpdate", "StoreRestartRegistrationRequested", "BeforeStoreDownloadAndInstall=true");
            if (!_restartRegistrationService.TryRegister(out var registrationFailure))
            {
                AppLogger.Default.Info("StoreUpdate", "StoreRestartRegistrationFailed", registrationFailure ?? "Unknown");
                Info = Info with { State = AppUpdateState.UpdateAvailable, Detail = registrationFailure, ErrorCode = "StoreRestartRegistrationFailed" };
                return;
            }

            restartRegistered = true;
            AppLogger.Default.Info("StoreUpdate", "StoreRestartRegistrationSucceeded", "BeforeStoreDownloadAndInstall=true");
            var progress = new Progress<AppUpdateProgress>(value =>
            {
                if (value.State is not (AppUpdateState.Downloading or AppUpdateState.Installing)) return;
                Progress = value.State == AppUpdateState.Downloading ? AppUpdateProgress.NormalizeValue(value.Value) : null;
                Info = Info with { State = value.State, Detail = value.Detail, ErrorCode = null };
                OnChanged(nameof(Progress));
            });
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(_sessionLifetime.Token, cancellationToken);
            var result = await _service.DownloadAndInstallAsync(progress, linked.Token);
            AppLogger.Default.Info("StoreUpdate", "StoreUpdateReturned", $"State={result.State}");

            if (result.State == AppUpdateState.Cancelled)
            {
                _restartRegistrationService.Unregister();
                restartRegistered = false;
                AppLogger.Default.Info("StoreUpdate", "StoreUpdateCancelled", "RestartRegistrationRemoved=true");
                Info = Info with { State = AppUpdateState.UpdateAvailable, Detail = null, ErrorCode = null };
                return;
            }
            if (result.State != AppUpdateState.Completed)
            {
                _restartRegistrationService.Unregister();
                restartRegistered = false;
                AppLogger.Default.Info("StoreUpdate", "StoreUpdateFailed", $"State={result.State};RestartRegistrationRemoved=true");
                Info = Info with { State = result.State, Detail = result.Detail, ErrorCode = result.ErrorCode };
                return;
            }

            // If Store deployment terminated us, this continuation never executes and Windows
            // relaunches through the registration above. A surviving process uses this fallback.
            _restartRegistrationService.Unregister();
            restartRegistered = false;
            AppLogger.Default.Info("StoreUpdate", "FallbackRestartRequested", "Store update completed while process remained alive.");
            if (!_restartService.TryRestart(out var failureReason))
            {
                RestartFailureReason = string.IsNullOrWhiteSpace(failureReason) ? "FallbackRestartFailed" : failureReason;
                AppLogger.Default.Info("StoreUpdate", "FallbackRestartFailed", RestartFailureReason);
                Info = Info with { State = AppUpdateState.Failed, Detail = RestartFailureReason, ErrorCode = "FallbackRestartFailed" };
            }
        }
        catch (OperationCanceledException)
        {
            if (restartRegistered) _restartRegistrationService.Unregister();
            AppLogger.Default.Info("StoreUpdate", "StoreUpdateCancelled", "OperationCanceledException");
            Info = Info with { State = AppUpdateState.UpdateAvailable, Detail = null, ErrorCode = null };
        }
        catch
        {
            if (restartRegistered) _restartRegistrationService.Unregister();
            throw;
        }
        finally
        {
            Progress = null;
            OnChanged(nameof(Progress));
            Interlocked.Exchange(ref _busy, 0);
            OnChanged(nameof(CanCheck));
            OnChanged(nameof(CanInstall));
        }
    }

    public Task RestartAndUpdateAsync(CancellationToken cancellationToken = default)
    {
        if (!Info.NeedsFinalRestart || Interlocked.Exchange(ref _busy, 1) != 0) return Task.CompletedTask;
        try
        {
            RestartFailureReason = null;
            if (Info.IsReadyToInstall)
            {
                return InstallPendingAndRestartAsync(cancellationToken);
            }

            Info = Info with { State = AppUpdateState.Restarting, Detail = null, ErrorCode = null };
            Progress = null;
            OnChanged(nameof(Progress));
            if (!_restartService.TryRestart(out var failureReason))
            {
                RestartFailureReason = string.IsNullOrWhiteSpace(failureReason) ? "RestartFailed" : failureReason;
                Info = Info with { State = AppUpdateState.RestartRequired, Detail = RestartFailureReason };
            }
        }
        finally
        {
            if (!Info.IsReadyToInstall) Interlocked.Exchange(ref _busy, 0);
            OnChanged(nameof(CanCheck));
            OnChanged(nameof(CanInstall));
        }
        return Task.CompletedTask;
    }

    private async Task InstallPendingAndRestartAsync(CancellationToken cancellationToken)
    {
        var storeRelaunchRegistered = false;
        try
        {
            if (Info.Source == UpdateInstallSource.Store)
            {
                if (!_restartRegistrationService.TryRegister(out var registrationFailure))
                {
                    Info = Info with
                    {
                        State = AppUpdateState.ReadyToInstall,
                        Detail = registrationFailure,
                        ErrorCode = "StoreRestartRegistrationFailed"
                    };
                    return;
                }

                storeRelaunchRegistered = true;
            }

            Info = Info with { State = AppUpdateState.Installing, Detail = null, ErrorCode = null };
            Progress = null;
            OnChanged(nameof(Progress));
            var progress = new Progress<AppUpdateProgress>(value =>
            {
                if (value.State is AppUpdateState.Downloading or AppUpdateState.Installing)
                {
                    Progress = value.State == AppUpdateState.Downloading ? AppUpdateProgress.NormalizeValue(value.Value) : null;
                    Info = Info with { State = value.State, Detail = value.Detail };
                    OnChanged(nameof(Progress));
                }
            });
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(_sessionLifetime.Token, cancellationToken);
            AppLogger.Default.Info("StoreUpdate", "StoreInstallRequested", "RelaunchRegistrationActive=" + storeRelaunchRegistered);
            var result = await _service.InstallPendingAsync(progress, linked.Token);
            AppLogger.Default.Info("StoreUpdate", "StoreInstallReturned", $"State={result.State}");

            if (storeRelaunchRegistered && result.State == AppUpdateState.Cancelled)
            {
                _restartRegistrationService.Unregister();
                storeRelaunchRegistered = false;
                Info = Info with { State = AppUpdateState.ReadyToInstall, Detail = null, ErrorCode = null };
                return;
            }

            if (storeRelaunchRegistered && result.State != AppUpdateState.Completed)
            {
                _restartRegistrationService.Unregister();
                storeRelaunchRegistered = false;
            }

            Info = Info with { State = result.State, Detail = result.Detail, ErrorCode = result.ErrorCode };

            // Store may terminate the app before its await continuation runs. When it does,
            // Windows owns relaunch through the registration made before deployment. If this
            // process survives a completed Store operation, it must take the fallback path.
            if (storeRelaunchRegistered && result.State == AppUpdateState.Completed)
            {
                _restartRegistrationService.Unregister();
                storeRelaunchRegistered = false;
                AppLogger.Default.Info("StoreUpdate", "FallbackRestartRequested", "Store install returned while process is alive.");
                if (!_restartService.TryRestart(out var failureReason))
                {
                    RestartFailureReason = string.IsNullOrWhiteSpace(failureReason) ? "FallbackRestartFailed" : failureReason;
                    AppLogger.Default.Info("StoreUpdate", "FallbackRestartFailed", "Restart service returned to the surviving process.");
                    Info = Info with { State = AppUpdateState.Failed, Detail = RestartFailureReason, ErrorCode = "FallbackRestartFailed" };
                }
            }

            // GitHub retains its independent deployment-and-restart path.
            if (result.State == AppUpdateState.RestartRequired)
                await RestartAndUpdateAsync(linked.Token);
        }
        catch (OperationCanceledException)
        {
            if (storeRelaunchRegistered) _restartRegistrationService.Unregister();
            Info = Info.Source == UpdateInstallSource.Store
                ? Info with { State = AppUpdateState.ReadyToInstall, Detail = null, ErrorCode = null }
                : Info with { State = AppUpdateState.Cancelled, Detail = "Cancelled" };
        }
        catch
        {
            if (storeRelaunchRegistered) _restartRegistrationService.Unregister();
            throw;
        }
        finally
        {
            Interlocked.Exchange(ref _busy, 0);
            OnChanged(nameof(CanCheck));
            OnChanged(nameof(CanInstall));
        }
    }

    // Progress callbacks can be posted after the awaited operation has completed.
    // They report activity only; the returned AppUpdateResult is authoritative for terminal transitions.
    private static bool CanApplyProgress(AppUpdateState current, AppUpdateState incoming) => incoming switch
    {
        AppUpdateState.Downloading => current is AppUpdateState.UpdateAvailable or AppUpdateState.Downloading,
        AppUpdateState.Verifying => current is AppUpdateState.Downloading or AppUpdateState.Verifying,
        AppUpdateState.Installing => current is AppUpdateState.UpdateAvailable or AppUpdateState.ReadyToInstall or AppUpdateState.Installing,
        AppUpdateState.RestartRequired => current is AppUpdateState.Installing or AppUpdateState.RestartRequired,
        AppUpdateState.Restarting => current is AppUpdateState.Installing or AppUpdateState.Restarting,
        AppUpdateState.Completed or AppUpdateState.Failed or AppUpdateState.Cancelled => true,
        _ => false
    };

    private void OnChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));

    private sealed class NoOpApplicationRestartService : IApplicationRestartService
    {
        public bool TryRestart() => false;
        public bool TryRestart(out string? failureReason) { failureReason = "RestartServiceUnavailable"; return false; }
    }

    private sealed class NoOpApplicationRestartRegistrationService : IApplicationRestartRegistrationService
    {
        public bool TryRegister(out string? failureReason) { failureReason = "RestartRegistrationServiceUnavailable"; return false; }
        public void Unregister() { }
    }
}
