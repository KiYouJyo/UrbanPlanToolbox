using UrbanPlanToolbox.Services;
using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class LanguageRestartPromptCoordinatorTests
{
    [Fact]
    public void SameLanguageDoesNotOpenPrompt()
    {
        var coordinator = new LanguageRestartPromptCoordinator();
        Assert.False(coordinator.TryBegin("zh-CN", "zh-CN"));
    }

    [Fact]
    public void DifferentLanguageOpensOnlyOnePromptUntilCompleted()
    {
        var coordinator = new LanguageRestartPromptCoordinator();
        Assert.True(coordinator.TryBegin("zh-CN", "ja-JP"));
        Assert.False(coordinator.TryBegin("ja-JP", "en-US"));
        var restart = new RestartStub(true);
        Assert.True(coordinator.Complete(false, restart));
        Assert.Equal(0, restart.CallCount);
        Assert.True(coordinator.TryBegin("ja-JP", "en-US"));
    }

    [Fact]
    public void RestartNowInvokesServiceOnceAndReleasesPromptOnFailure()
    {
        var coordinator = new LanguageRestartPromptCoordinator();
        var restart = new RestartStub(false);
        Assert.True(coordinator.TryBegin("zh-CN", "ja-JP"));
        Assert.False(coordinator.Complete(true, restart));
        Assert.Equal(1, restart.CallCount);
        Assert.True(coordinator.TryBegin("ja-JP", "en-US"));
    }

    private sealed class RestartStub(bool result) : IApplicationRestartService
    {
        public int CallCount { get; private set; }
        public bool TryRestart() { CallCount++; return result; }
        public bool TryRestart(out string? failureReason) { failureReason = result ? null : "StubFailure"; CallCount++; return result; }
    }
}
