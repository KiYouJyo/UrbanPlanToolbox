using UrbanPlanToolbox.Models.Projects;

namespace UrbanPlanToolbox.Services;

/// <summary>Pure reminder selection and default-time policy, independent of the Windows notification shell.</summary>
public static class MilestoneReminderPlanner
{
    public static IReadOnlyList<MilestoneReminder> Create(IEnumerable<ProjectRecord> projects, DateTimeOffset now) =>
        projects.Where(project => !project.IsArchived)
            .SelectMany(project => project.Milestones.Where(milestone => milestone.ReminderEnabled).Select(milestone => Create(project, milestone)))
            .Where(reminder => reminder.DueAtLocal > now)
            .OrderBy(reminder => reminder.DueAtLocal)
            .ToArray();

    private static MilestoneReminder Create(ProjectRecord project, ProjectMilestone milestone)
    {
        var time = milestone.Time ?? new TimeOnly(9, 0);
        var local = milestone.Date.ToDateTime(time);
        return new(project.Id, milestone.Id, project.Name, milestone.Title,
            new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local)), milestone.Time.HasValue);
    }
}

public sealed record MilestoneReminder(Guid ProjectId, Guid MilestoneId, string ProjectName, string MilestoneTitle, DateTimeOffset DueAtLocal, bool HasExplicitTime);
