using UrbanPlanToolbox.Models.Interaction;

namespace UrbanPlanToolbox.Services;

/// <summary>
/// Prevents duplicate execution for one named operation without serializing unrelated work.
/// Consumers must still marshal UI updates to their own dispatcher.
/// </summary>
public sealed class AsyncOperationRunner
{
    private readonly object _gate = new();
    private readonly Dictionary<string, OperationState> _states = new(StringComparer.Ordinal);

    public event EventHandler<OperationStateChangedEventArgs>? StateChanged;

    public OperationState GetState(string operationKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);
        lock (_gate) return _states.GetValueOrDefault(operationKey, OperationState.Idle);
    }

    public async Task<AsyncOperationResult> RunAsync(string operationKey, Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);
        ArgumentNullException.ThrowIfNull(action);
        lock (_gate)
        {
            if (_states.GetValueOrDefault(operationKey) == OperationState.Running)
                return AsyncOperationResult.AlreadyRunning;
            _states[operationKey] = OperationState.Running;
        }
        OnStateChanged(operationKey, OperationState.Running);

        try
        {
            await action(cancellationToken).ConfigureAwait(false);
            SetState(operationKey, OperationState.Succeeded);
            return AsyncOperationResult.Succeeded;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            SetState(operationKey, OperationState.Canceled);
            return AsyncOperationResult.Canceled;
        }
        catch (Exception exception)
        {
            SetState(operationKey, OperationState.Failed);
            return AsyncOperationResult.Failed(exception);
        }
    }

    private void SetState(string operationKey, OperationState state)
    {
        lock (_gate) _states[operationKey] = state;
        OnStateChanged(operationKey, state);
    }

    private void OnStateChanged(string key, OperationState state) =>
        StateChanged?.Invoke(this, new OperationStateChangedEventArgs(key, state));
}

public sealed class OperationStateChangedEventArgs : EventArgs
{
    public OperationStateChangedEventArgs(string operationKey, OperationState state)
    {
        OperationKey = operationKey;
        State = state;
    }

    public string OperationKey { get; }
    public OperationState State { get; }
}

public sealed record AsyncOperationResult(bool Started, OperationState State, Exception? Exception = null)
{
    public static AsyncOperationResult AlreadyRunning { get; } = new(false, OperationState.Running);
    public static AsyncOperationResult Succeeded { get; } = new(true, OperationState.Succeeded);
    public static AsyncOperationResult Canceled { get; } = new(true, OperationState.Canceled);
    public static AsyncOperationResult Failed(Exception exception) => new(true, OperationState.Failed, exception);
}
