using UrbanPlanToolbox.Models.Projects;

namespace UrbanPlanToolbox.Services;

/// <summary>Converts legacy per-milestone reminder choices to the app-level setting.</summary>
public static class MilestoneReminderMigration
{
    public static bool DetermineEnabled(IEnumerable<ProjectRecord> projects) =>
        projects.SelectMany(project => project.Milestones).Any(milestone => milestone.ReminderEnabled);
}
