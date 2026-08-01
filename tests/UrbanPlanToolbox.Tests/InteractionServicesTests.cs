using UrbanPlanToolbox.Models.Interaction;
using UrbanPlanToolbox.Services;
using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class InteractionServicesTests
{
    [Fact]
    public async Task AsyncRunnerRejectsDuplicateButAllowsIndependentOperations()
    {
        var runner = new AsyncOperationRunner();
        var gate = new TaskCompletionSource();
        var first = runner.RunAsync("export", async _ => await gate.Task);
        var duplicate = await runner.RunAsync("export", _ => Task.CompletedTask);
        var independent = await runner.RunAsync("update", _ => Task.CompletedTask);

        Assert.False(duplicate.Started);
        Assert.Equal(OperationState.Running, runner.GetState("export"));
        Assert.Equal(OperationState.Succeeded, independent.State);
        gate.SetResult();
        Assert.Equal(OperationState.Succeeded, (await first).State);
    }

    [Fact]
    public async Task AsyncRunnerRestoresStateAfterFailureAndCancellation()
    {
        var runner = new AsyncOperationRunner();
        var failed = await runner.RunAsync("save", _ => Task.FromException(new InvalidOperationException()));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var canceled = await runner.RunAsync("save", _ => Task.FromCanceled(cancellation.Token), cancellation.Token);

        Assert.Equal(OperationState.Failed, failed.State);
        Assert.IsType<InvalidOperationException>(failed.Exception);
        Assert.Equal(OperationState.Canceled, canceled.State);
    }

    [Theory]
    [InlineData(UnsavedChangesDecision.DiscardAndContinue, true, 0)]
    [InlineData(UnsavedChangesDecision.Cancel, false, 0)]
    [InlineData(UnsavedChangesDecision.SaveAndContinue, true, 1)]
    public async Task UnsavedChangesGuardHonorsDecision(UnsavedChangesDecision decision, bool expected, int saves)
    {
        var guard = new UnsavedChangesGuard();
        var saveCount = 0;
        var result = await guard.CanContinueAsync(true, _ => Task.FromResult(decision), _ => { saveCount++; return Task.FromResult(true); });

        Assert.Equal(expected, result);
        Assert.Equal(saves, saveCount);
    }

    [Fact]
    public async Task UnsavedChangesGuardDoesNotContinueAfterFailedSave()
    {
        var guard = new UnsavedChangesGuard();
        var result = await guard.CanContinueAsync(true, _ => Task.FromResult(UnsavedChangesDecision.SaveAndContinue), _ => Task.FromResult(false));
        Assert.False(result);
    }

    [Fact]
    public void NotificationsDeduplicateRapidEquivalentMessagesButKeepErrorsPersistent()
    {
        var service = new AppNotificationService();
        var raised = 0;
        service.NotificationRaised += (_, _) => raised++;
        var error = new AppNotification(AppNotificationKind.Error, "Failure", "Safe error", true);

        Assert.True(service.Notify(error));
        Assert.False(service.Notify(error));
        Assert.Equal(1, raised);
        Assert.True(error.IsPersistent);
    }
}
