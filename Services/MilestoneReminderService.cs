using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using UrbanPlanToolbox.Models.Projects;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace UrbanPlanToolbox.Services;

/// <summary>Schedules one local Windows notification for each future, active project milestone.</summary>
public sealed class MilestoneReminderService
{
    private const string ReminderGroup = "UrbanPlanToolbox.Milestones";
    private readonly ProjectStorageService _projects;
    private readonly ILocalizationService _localization;
    private readonly Func<DateTimeOffset> _now;
    private bool _registered;

    public static MilestoneReminderService Default { get; } = new(ProjectStorageService.Default, LocalizationService.Default);

    public MilestoneReminderService(ProjectStorageService projects, ILocalizationService localization, Func<DateTimeOffset>? now = null)
    {
        _projects = projects;
        _localization = localization;
        _now = now ?? (() => DateTimeOffset.Now);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var list = await _projects.ListAsync(false, cancellationToken).ConfigureAwait(false);
        var reminders = MilestoneReminderPlanner.Create(list.Projects, _now());
        try
        {
            EnsureRegistered();
            var notifier = ToastNotificationManager.CreateToastNotifier();
            foreach (var scheduled in notifier.GetScheduledToastNotifications().Where(item => item.Group == ReminderGroup).ToArray())
                notifier.RemoveFromSchedule(scheduled);

            foreach (var reminder in reminders)
            {
                var document = new XmlDocument();
                document.LoadXml(BuildNotification(reminder).Payload);
                var scheduled = new ScheduledToastNotification(document, reminder.DueAtLocal)
                {
                    Tag = reminder.MilestoneId.ToString("N"),
                    Group = ReminderGroup
                };
                notifier.AddToSchedule(scheduled);
            }
        }
        catch (Exception) when (OperatingSystem.IsWindows())
        {
            // Notification permission or shell availability must never block project persistence or startup.
        }
    }

    private AppNotification BuildNotification(MilestoneReminder reminder)
    {
        var due = reminder.HasExplicitTime
            ? reminder.DueAtLocal.ToString("g")
            : reminder.DueAtLocal.ToString("d");
        return new AppNotificationBuilder()
            .AddArgument("action", "openMilestone")
            .AddArgument("projectId", reminder.ProjectId.ToString("D"))
            .AddArgument("milestoneId", reminder.MilestoneId.ToString("D"))
            .AddText(_localization.GetString("Reminder_NotificationTitle"))
            .AddText(_localization.GetFormattedString("Reminder_NotificationBody", reminder.ProjectName, reminder.MilestoneTitle, due))
            .BuildNotification();
    }

    private void EnsureRegistered()
    {
        if (_registered) return;
        AppNotificationManager.Default.Register();
        _registered = true;
    }
}
