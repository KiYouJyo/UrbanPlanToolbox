# Project milestone reminders

UrbanPlanToolbox schedules future, enabled milestones from active projects as local Windows notifications. It does not use a cloud service, WNS, Azure, external attachments, or external paths.

- A milestone with no explicit time is scheduled for 09:00 local time.
- Each milestone has its own reminder toggle. Saving an edit, archiving/restoring a project, deleting a milestone/project, importing a backup, changing language, and application startup reconcile the future schedule.
- Stable identifiers derived from `ProjectId` and `MilestoneId` are used for Windows notification group/tag values. The app removes its legacy and current scheduled entries before rebuilding, which prevents duplicates and cancels entries absent from imported data.
- Registration and scheduling failures do not block project persistence. The project page shows a non-blocking warning and writes diagnostic output for a debugger; Windows notification permissions, Focus/Do Not Disturb, or shell policy can still prevent delivery.

The implementation uses Windows App SDK `AppNotificationManager` for registration and the packaged-app `ScheduledToastNotification` queue for local scheduled delivery. Notification activation carries the project and milestone IDs and launches the application; no remote action is performed.

`ReminderEnabled` is an optional, backward-compatible project JSON property with a default of `true`; existing v3 project files, backups, recovery data, and imports therefore remain readable without a schema-version bump.
