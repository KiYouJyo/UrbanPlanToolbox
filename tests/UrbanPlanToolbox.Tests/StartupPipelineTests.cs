using UrbanPlanToolbox.Services;
using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class StartupPipelineTests
{
    [Fact]
    public async Task BackgroundOperationsRunAfterPipelineStartsAndFailureDoesNotEscape()
    {
        var pipeline = new StartupPipeline();
        var notificationCalls = 0;
        var failures = new List<Exception>();
        await pipeline.RunAfterFirstFrameAsync(
            () => { notificationCalls++; throw new InvalidOperationException("expected"); },
            () => Task.CompletedTask,
            failures.Add);
        await pipeline.RunAfterFirstFrameAsync(() => { notificationCalls++; return Task.CompletedTask; }, () => Task.CompletedTask, failures.Add);
        Assert.True(pipeline.HasStarted);
        Assert.Equal(1, notificationCalls);
        Assert.Single(failures);
    }

    [Fact]
    public void TimingRecordsElapsedTimeAndThread()
    {
        var timing = new StartupTiming();
        timing.Mark("critical");
        var point = Assert.Single(timing.Points);
        Assert.Equal("critical", point.Name);
        Assert.True(point.ElapsedMilliseconds >= 0);
        Assert.True(point.ThreadId > 0);
    }
}
