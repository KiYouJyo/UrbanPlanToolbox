using UrbanPlanToolbox.Models.Projects;
using UrbanPlanToolbox.Models;
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

    [Fact]
    public async Task LegacyPerMilestoneFlagIsReadableButDoesNotControlGlobalScheduling()
    {
        using var scope = new ReminderScope();
        var project = (await scope.Projects.CreateAsync("Active", ProjectTypeCodes.Coursework)).Project!;
        await scope.Projects.AddMilestoneAsync(project.Id, "Disabled", new DateOnly(2026, 8, 2), reminderEnabled: false);
        await scope.Projects.AddMilestoneAsync(project.Id, "Enabled", new DateOnly(2026, 8, 3));

        var reminders = MilestoneReminderPlanner.Create((await scope.Projects.ListAsync(false)).Projects, new DateTimeOffset(2026, 8, 1, 8, 0, 0, TimeSpan.Zero));
        Assert.Equal(2, reminders.Count);
        Assert.Contains(reminders, reminder => reminder.MilestoneTitle == "Disabled");
        Assert.Contains(reminders, reminder => reminder.MilestoneTitle == "Enabled");
        Assert.True((await scope.Projects.ReadAsync(project.Id)).Value!.Milestones.Single(item => item.Title == "Enabled").ReminderEnabled);
    }

    [Fact]
    public void LegacyMigrationEnablesNotificationsWhenAnyMilestoneWasEnabled()
    {
        var projects = new[]
        {
            new ProjectRecord
            {
                Id = Guid.NewGuid(), Name = "Legacy", Type = ProjectTypeCodes.Coursework,
                CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow,
                Milestones = { CreateMilestone(true) }
            }
        };

        Assert.True(MilestoneReminderMigration.DetermineEnabled(projects));
    }

    [Fact]
    public void LegacyMigrationDisablesNotificationsWhenNoMilestoneWasEnabled()
    {
        var projects = new[]
        {
            new ProjectRecord
            {
                Id = Guid.NewGuid(), Name = "Legacy", Type = ProjectTypeCodes.Coursework,
                CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow,
                Milestones = { CreateMilestone(false) }
            }
        };

        Assert.False(MilestoneReminderMigration.DetermineEnabled(projects));
    }

    private static ProjectMilestone CreateMilestone(bool enabled) => new()
    {
        Id = Guid.NewGuid(), Title = "Legacy milestone", Date = new DateOnly(2026, 8, 2), ReminderEnabled = enabled,
        CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow
    };

    [Fact]
    public void ShellIdentityIsStableDistinctAndWithinWindowsLimit()
    {
        var project = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var milestone = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        Assert.Equal(MilestoneReminderIdentity.Group(project), MilestoneReminderIdentity.Group(project));
        Assert.Equal(MilestoneReminderIdentity.Tag(milestone), MilestoneReminderIdentity.Tag(milestone));
        Assert.True(MilestoneReminderIdentity.Group(project).Length <= 16);
        Assert.True(MilestoneReminderIdentity.Tag(milestone).Length <= 16);
        Assert.NotEqual(MilestoneReminderIdentity.Tag(milestone), MilestoneReminderIdentity.Tag(Guid.NewGuid()));
        Assert.NotEqual(MilestoneReminderIdentity.Tag(milestone, 1), MilestoneReminderIdentity.Tag(milestone, 2));
        Assert.NotEqual(MilestoneReminderIdentity.Tag(milestone), MilestoneReminderIdentity.Tag(milestone, 1));
    }

    [Theory]
    [InlineData(MilestoneReminderRepeatInterval.Hours6, 6)]
    [InlineData(MilestoneReminderRepeatInterval.Hours12, 12)]
    [InlineData(MilestoneReminderRepeatInterval.Hours24, 24)]
    public void RepeatIntervalsUseStableDelays(MilestoneReminderRepeatInterval interval, int hours)
    {
        Assert.Equal(TimeSpan.FromHours(hours), MilestoneReminderPlanner.GetRepeatDelay(interval));
    }

    [Fact]
    public void ThreeDayIntervalUsesThreeDayDelay() =>
        Assert.Equal(TimeSpan.FromDays(3), MilestoneReminderPlanner.GetRepeatDelay(MilestoneReminderRepeatInterval.Days3));

    [Fact]
    public void RepeatPlannerCreatesPrimaryAndAtMostThreeFutureRepeats()
    {
        var now = new DateTimeOffset(2026, 8, 1, 8, 0, 0, TimeSpan.Zero);
        var project = new ProjectRecord
        {
            Id = Guid.NewGuid(), Name = "Project", Type = ProjectTypeCodes.Coursework,
            CreatedAtUtc = now, UpdatedAtUtc = now,
            Milestones = { new ProjectMilestone { Id = Guid.NewGuid(), Title = "Review", Date = new DateOnly(2026, 8, 2), CreatedAtUtc = now, UpdatedAtUtc = now } }
        };

        var reminders = MilestoneReminderPlanner.Create([project], now, MilestoneReminderRepeatInterval.Hours6);

        Assert.Equal(4, reminders.Count);
        Assert.Equal([0, 1, 2, 3], reminders.Select(item => item.RepeatIndex).ToArray());
        Assert.Equal(TimeSpan.FromHours(6), reminders[1].DueAtLocal - reminders[0].DueAtLocal);
        Assert.Equal(TimeSpan.FromHours(18), reminders[3].DueAtLocal - reminders[0].DueAtLocal);
    }

    [Fact]
    public void RepeatPlannerSkipsPastOccurrencesButKeepsFutureSeries()
    {
        var now = new DateTimeOffset(2026, 8, 3, 10, 0, 0, TimeSpan.Zero);
        var project = new ProjectRecord
        {
            Id = Guid.NewGuid(), Name = "Project", Type = ProjectTypeCodes.Coursework,
            CreatedAtUtc = now, UpdatedAtUtc = now,
            Milestones = { new ProjectMilestone { Id = Guid.NewGuid(), Title = "Review", Date = new DateOnly(2026, 8, 2), Time = new TimeOnly(9, 0), CreatedAtUtc = now, UpdatedAtUtc = now } }
        };

        var reminders = MilestoneReminderPlanner.Create([project], now, MilestoneReminderRepeatInterval.Hours24);

        Assert.Equal([2, 3], reminders.Select(item => item.RepeatIndex).ToArray());
    }

    [Fact]
    public void UnknownRepeatSettingSafelyNormalizesToNone()
    {
        var settings = new AppSettings { ProjectMilestoneReminderRepeatInterval = (MilestoneReminderRepeatInterval)999 };
        Assert.Equal(MilestoneReminderRepeatInterval.None, settings.NormalizedProjectMilestoneReminderRepeatInterval);
        Assert.Null(MilestoneReminderPlanner.GetRepeatDelay(MilestoneReminderRepeatInterval.None));
    }

    [Fact]
    public void SchedulingFailureRetainsSpecificReasonAndHresult()
    {
        var result = MilestoneReminderRefreshResult.Failure(new InvalidOperationException("Manifest COM registration is missing."));
        Assert.False(result.Succeeded);
        Assert.Contains("Manifest COM registration is missing.", result.Diagnostic);
        Assert.Contains("0x", result.Diagnostic);
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
