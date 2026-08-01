using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using UrbanPlanToolbox.Models.Projects;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace UrbanPlanToolbox.Services;

/// <summary>Schedules one local Windows notification for each future, active project milestone.</summary>
public sealed class MilestoneReminderService
{
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

    public async Task<MilestoneReminderRefreshResult> RefreshAsync(CancellationToken cancellationToken = default)
    {
        var list = await _projects.ListAsync(false, cancellationToken).ConfigureAwait(false);
        var reminders = MilestoneReminderPlanner.Create(list.Projects, _now());
        try
        {
            EnsureRegistered();
            var notifier = ToastNotificationManager.CreateToastNotifier();
            foreach (var scheduled in notifier.GetScheduledToastNotifications().Where(IsOwnedSchedule).ToArray())
                notifier.RemoveFromSchedule(scheduled);

            foreach (var reminder in reminders)
            {
                var document = new XmlDocument();
                document.LoadXml(BuildNotification(reminder).Payload);
                var scheduled = new ScheduledToastNotification(document, reminder.DueAtLocal)
                {
                    Tag = MilestoneReminderIdentity.Tag(reminder.MilestoneId),
                    Group = MilestoneReminderIdentity.Group(reminder.ProjectId)
                };
                notifier.AddToSchedule(scheduled);
            }
            return MilestoneReminderRefreshResult.Success(reminders.Count);
        }
        catch (Exception exception) when (OperatingSystem.IsWindows())
        {
            // Scheduling is deliberately non-blocking, but callers receive a diagnostic result.
            System.Diagnostics.Debug.WriteLine($"Milestone reminder scheduling failed: {exception}");
            return MilestoneReminderRefreshResult.Failure(exception);
        }
    }

    public MilestoneReminderRefreshResult SendTestNotification()
    {
        try
        {
            EnsureRegistered();
            AppNotificationManager.Default.Show(new AppNotificationBuilder()
                .AddArgument("action", "testNotification")
                .AddText(_localization.GetString("Notification_Test_Title"))
                .AddText(_localization.GetString("Notification_Test_Body"))
                .BuildNotification());
            return MilestoneReminderRefreshResult.Success(1);
        }
        catch (Exception exception) when (OperatingSystem.IsWindows())
        {
            System.Diagnostics.Debug.WriteLine($"Test notification failed: {exception}");
            return MilestoneReminderRefreshResult.Failure(exception);
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
        if (IsElevated()) throw new InvalidOperationException("Windows app notifications are unavailable while the application is running as administrator.");
        AppNotificationManager.Default.NotificationInvoked += OnNotificationInvoked;
        AppNotificationManager.Default.Register();
        _registered = true;
    }

    private static bool IsElevated()
    {
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        return new System.Security.Principal.WindowsPrincipal(identity).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }

    private static void OnNotificationInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
    {
        // The packaged COM activation declaration launches this EXE. App startup remains the safe
        // destination for all notification actions; no project content is logged here.
        System.Diagnostics.Debug.WriteLine("UrbanPlanToolbox notification activated.");
    }

    private static bool IsOwnedSchedule(ScheduledToastNotification item) =>
        item.Group == MilestoneReminderIdentity.LegacyGroup ||
        item.Group.StartsWith(MilestoneReminderIdentity.GroupPrefix, StringComparison.Ordinal);
}
