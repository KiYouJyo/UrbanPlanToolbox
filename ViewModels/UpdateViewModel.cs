using System.ComponentModel;
using System.Runtime.CompilerServices;
using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Services;

namespace UrbanPlanToolbox.ViewModels;

public sealed class UpdateViewModel(IAppUpdateService service, IApplicationRestartService? restartService = null) : INotifyPropertyChanged
{
    private readonly IAppUpdateService _service = service;
    private readonly IApplicationRestartService _restartService = restartService ?? new NoOpApplicationRestartService();
    private int _busy;
    private AppUpdateInfo _info = new(AppUpdateState.NotChecked);
    public event PropertyChangedEventHandler? PropertyChanged;
    public AppUpdateInfo Info { get => _info; private set { _info = value; OnChanged(); OnChanged(nameof(CanCheck)); OnChanged(nameof(CanInstall)); } }
    public double? Progress { get; private set; }
    public string? RestartFailureReason { get; private set; }
    public bool CanCheck => Volatile.Read(ref _busy) == 0;
    public bool CanInstall => CanCheck && (Info.IsUpdateAvailable || Info.NeedsFinalRestart);
    public string CurrentVersion => AppVersionProvider.DisplayVersion;
    public bool ShouldShowUpdateDialog => Info.IsUpdateAvailable;
    public async Task SetLocalizedNotesAsync(IReleaseNotesProvider provider, string locale, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(Info.AvailableVersion)) return;
        var notes = await provider.GetAsync(Info.AvailableVersion, locale, cancellationToken);
        if (notes is not null) Info = Info with { LocalizedReleaseNotes = notes, ReleaseNotes = null };
    }

    public async Task CheckAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _busy, 1) != 0) return;
        try
        {
            Progress = null;
            OnChanged(nameof(Progress));
            Info = Info with { State = AppUpdateState.Checking, Detail = null, ErrorCode = null };
            var result = await _service.CheckForUpdatesAsync(cancellationToken);
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
            var result = Info.IsReadyToInstall
                ? await _service.InstallPendingAsync(progress, cancellationToken)
                : await _service.DownloadAndInstallAsync(progress, cancellationToken);
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
        try
        {
            var result = await _service.InstallPendingAsync(cancellationToken: cancellationToken);
            Info = Info with { State = result.State, Detail = result.Detail, ErrorCode = result.ErrorCode };
        }
        catch (OperationCanceledException) { Info = Info with { State = AppUpdateState.Cancelled, Detail = "Cancelled" }; }
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
        AppUpdateState.Installing => current is AppUpdateState.ReadyToInstall or AppUpdateState.Installing,
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
}
