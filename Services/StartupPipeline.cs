namespace UrbanPlanToolbox.Services;

public sealed class StartupPipeline
{
    private int _started;
    public bool HasStarted => Volatile.Read(ref _started) != 0;
    public async Task RunAfterFirstFrameAsync(Func<Task> notificationRefresh, Func<Task> logMaintenance, Action<Exception>? onFailure = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notificationRefresh); ArgumentNullException.ThrowIfNull(logMaintenance);
        if (Interlocked.Exchange(ref _started, 1) != 0) return;
        var maintenance = RunSafeAsync(logMaintenance, onFailure, cancellationToken);
        await RunSafeAsync(notificationRefresh, onFailure, cancellationToken).ConfigureAwait(false);
        await maintenance.ConfigureAwait(false);
    }
    private static async Task RunSafeAsync(Func<Task> operation, Action<Exception>? onFailure, CancellationToken cancellationToken)
    {
        try { await operation().WaitAsync(cancellationToken).ConfigureAwait(false); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception) { onFailure?.Invoke(exception); }
    }
}
