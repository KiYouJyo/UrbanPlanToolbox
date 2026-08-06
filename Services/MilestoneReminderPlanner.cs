using UrbanPlanToolbox.Models.Projects;
using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.Services;

/// <summary>Pure reminder selection and default-time policy, independent of the Windows notification shell.</summary>
public static class MilestoneReminderPlanner
{
    public const int MaxRepeatCount = 3;

    public static IReadOnlyList<MilestoneReminder> Create(
        IEnumerable<ProjectRecord> projects,
        DateTimeOffset now,
        MilestoneReminderRepeatInterval repeatInterval = MilestoneReminderRepeatInterval.None,
        bool enabled = true) =>
        !enabled ? [] :
        projects.Where(project => !project.IsArchived)
            .SelectMany(project => project.Milestones.SelectMany(milestone => Create(project, milestone, now, repeatInterval)))
            .OrderBy(reminder => reminder.DueAtLocal)
            .ThenBy(reminder => reminder.RepeatIndex)
            .ToArray();

    public static TimeSpan? GetRepeatDelay(MilestoneReminderRepeatInterval interval) => interval switch
    {
        MilestoneReminderRepeatInterval.Hours6 => TimeSpan.FromHours(6),
        MilestoneReminderRepeatInterval.Hours12 => TimeSpan.FromHours(12),
        MilestoneReminderRepeatInterval.Hours24 => TimeSpan.FromHours(24),
        MilestoneReminderRepeatInterval.Days3 => TimeSpan.FromDays(3),
        _ => null
    };

    private static IEnumerable<MilestoneReminder> Create(ProjectRecord project, ProjectMilestone milestone, DateTimeOffset now, MilestoneReminderRepeatInterval repeatInterval)
    {
        var time = milestone.Time ?? new TimeOnly(9, 0);
        var local = milestone.Date.ToDateTime(time);
        var primary = new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local));
        var delay = GetRepeatDelay(repeatInterval);
        for (var repeatIndex = 0; repeatIndex <= (delay.HasValue ? MaxRepeatCount : 0); repeatIndex++)
        {
            var due = primary + (delay ?? TimeSpan.Zero) * repeatIndex;
            if (due > now)
                yield return new(project.Id, milestone.Id, project.Name, milestone.Title, due, milestone.Time.HasValue, repeatIndex);
        }
    }
}

public sealed record MilestoneReminder(Guid ProjectId, Guid MilestoneId, string ProjectName, string MilestoneTitle, DateTimeOffset DueAtLocal, bool HasExplicitTime, int RepeatIndex);
