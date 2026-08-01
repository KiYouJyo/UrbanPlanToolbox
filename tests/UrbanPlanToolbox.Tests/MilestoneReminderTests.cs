using UrbanPlanToolbox.Models.Projects;
using UrbanPlanToolbox.Services;
using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class MilestoneReminderTests
{
    [Fact]
    public async Task SchedulesOnlyFutureMilestonesForActiveProjects()
    {
        using var scope = new ReminderScope();
        var now = new DateTimeOffset(2026, 8, 1, 8, 0, 0, TimeSpan.Zero);
        var active = (await scope.Projects.CreateAsync("Active", ProjectTypeCodes.Coursework)).Project!;
        await scope.Projects.AddMilestoneAsync(active.Id, "Due", new DateOnly(2026, 8, 2));
        var archived = (await scope.Projects.CreateAsync("Archived", ProjectTypeCodes.Coursework)).Project!;
        await scope.Projects.AddMilestoneAsync(archived.Id, "Ignore", new DateOnly(2026, 8, 2));
        await scope.Projects.ArchiveAsync(archived.Id, true);

        var reminders = MilestoneReminderPlanner.Create((await scope.Projects.ListAsync(false)).Projects, now);

        var reminder = Assert.Single(reminders);
        Assert.Equal("Due", reminder.MilestoneTitle);
        Assert.False(reminder.HasExplicitTime);
        Assert.Equal(9, reminder.DueAtLocal.Hour);
    }

    private sealed class ReminderScope : IDisposable
    {
        public ReminderScope()
        {
            Root = Path.Combine(Path.GetTempPath(), $"UrbanPlanToolbox-reminders-{Guid.NewGuid():N}");
            var paths = new AppDataPathProvider(Root, [Models.Tools.ToolIds.PlanningIndicatorCalculator, Models.Tools.ToolIds.UnitScaleConverter]);
            paths.EnsureInfrastructureDirectories();
            Projects = new ProjectStorageService(paths);
        }
        public string Root { get; }
        public ProjectStorageService Projects { get; }
        public void Dispose() { if (Directory.Exists(Root)) Directory.Delete(Root, true); }
    }
}
