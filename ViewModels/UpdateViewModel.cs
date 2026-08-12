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
    public bool CanInstall => CanCheck && Info.IsUpdateAvailable;
    public string CurrentVersion => AppVersionProvider.DisplayVersion;
    public bool ShouldShowUpdateDialog => Info.IsUpdateAvailable;
    public async Task SetLocalizedNotesAsync(IReleaseNotesProvider provider, string locale, CancellationToken cancellationToken = default)
    {
        if (!Info.IsUpdateAvailable || string.IsNullOrWhiteSpace(Info.AvailableVersion)) return;
        var notes = await provider.GetAsync(Info.AvailableVersion, locale, cancellationToken);
        if (notes is not null) Info = Info with { LocalizedReleaseNotes = notes, ReleaseNotes = null };
    }

    public async Task CheckAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _busy, 1) != 0) return;
        try { Progress = null; OnChanged(nameof(Progress)); Info = new(AppUpdateState.Checking); Info = await _service.CheckForUpdatesAsync(cancellationToken); }
        catch (OperationCanceledException) { Progress = null; OnChanged(nameof(Progress)); Info = new(AppUpdateState.Cancelled); }
        finally { Interlocked.Exchange(ref _busy, 0); OnChanged(nameof(CanCheck)); OnChanged(nameof(CanInstall)); }
    }

    public async Task DownloadAndInstallAsync(CancellationToken cancellationToken = default)
    {
        if (!CanInstall || Interlocked.Exchange(ref _busy, 1) != 0) return;
        try
        {
            RestartFailureReason = null;
            var progress = new Progress<AppUpdateProgress>(value =>
            {
                var normalized = AppUpdateProgress.NormalizeValue(value.Value);
                if (value.State == AppUpdateState.Downloading)
                {
                    if (normalized is double) Progress = normalized;
                }
                else if (value.State is AppUpdateState.Installing or AppUpdateState.Completed or AppUpdateState.Failed or AppUpdateState.Cancelled)
                {
                    Progress = value.State == AppUpdateState.Completed ? 1d : null;
                }

                Info = Info with { State = value.State, Detail = value.Detail };
                OnChanged(nameof(Progress));
            });
            var result = await _service.DownloadAndInstallAsync(progress, cancellationToken);
            var finalState = result.State;
            // Store and App Installer own shutdown/restart. The app must not ask for a second confirmation.
            Progress = null;
            OnChanged(nameof(Progress));
            Info = new(finalState, Detail: result.Detail, ErrorCode: result.ErrorCode);
        }
        catch (OperationCanceledException) { Progress = null; OnChanged(nameof(Progress)); Info = new(AppUpdateState.Cancelled); }
        finally { Interlocked.Exchange(ref _busy, 0); OnChanged(nameof(CanCheck)); OnChanged(nameof(CanInstall)); }
    }

    public bool TryRestartAgain() => _restartService.TryRestart(out _);

    private void OnChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));

    private sealed class NoOpApplicationRestartService : IApplicationRestartService
    {
        public bool TryRestart() => false;
        public bool TryRestart(out string? failureReason) { failureReason = "RestartServiceUnavailable"; return false; }
    }
}
