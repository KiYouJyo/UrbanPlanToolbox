using UrbanPlanToolbox.Models.Interaction;

namespace UrbanPlanToolbox.Services;

/// <summary>Coordinates a single dirty-page decision and never navigates after a failed save.</summary>
public sealed class UnsavedChangesGuard
{
    private int _prompting;

    public async Task<bool> CanContinueAsync(
        bool isDirty,
        Func<CancellationToken, Task<UnsavedChangesDecision>> requestDecision,
        Func<CancellationToken, Task<bool>> save,
        CancellationToken cancellationToken = default)
    {
        if (!isDirty) return true;
        if (Interlocked.Exchange(ref _prompting, 1) != 0) return false;
        try
        {
            return await requestDecision(cancellationToken).ConfigureAwait(false) switch
            {
                UnsavedChangesDecision.DiscardAndContinue => true,
                UnsavedChangesDecision.SaveAndContinue => await save(cancellationToken).ConfigureAwait(false),
                _ => false
            };
        }
        finally
        {
            Volatile.Write(ref _prompting, 0);
        }
    }
}
