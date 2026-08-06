# Project milestone reminders

UrbanPlanToolbox schedules future milestones from active projects as local Windows notifications when the application-level reminder setting is enabled. It does not use a cloud service, WNS, Azure, external attachments, or external paths.

- A milestone with no explicit time is scheduled for 09:00 local time.
- The Settings page stores a stable `MilestoneReminderRepeatInterval` value (`None`, `Hours6`, `Hours12`, `Hours24`, or `Days3`). A selected interval schedules at most three future repetitions after the primary reminder; past occurrences are skipped.
- Stable identifiers derived from `ProjectId`, `MilestoneId`, and the series index are used for Windows notification group/tag values. The app removes only its legacy/current project-milestone entries before rebuilding, which prevents duplicates and cancels entries absent from edited or imported data.
- Saving an edit, changing the repeat interval, archiving/restoring a project, deleting a milestone/project, importing a backup, changing language, and application startup reconcile the future schedule. Language changes only rebuild the UI and do not themselves trigger notification synchronization.
- Registration and scheduling failures do not block project persistence. The project page shows a non-blocking warning and writes diagnostic output for a debugger; Windows notification permissions, Focus/Do Not Disturb, or shell policy can still prevent delivery.

The implementation uses Windows App SDK `AppNotificationManager` for registration and the packaged-app `ScheduledToastNotification` queue for local scheduled delivery. Notification activation carries the project and milestone IDs and launches the application; no remote action is performed.

`ReminderEnabled` is an optional, backward-compatible project JSON property with a default of `true`; existing v3 project files, backups, recovery data, and imports therefore remain readable without a schema-version bump. It is no longer used to decide runtime scheduling. Settings files missing the application-level switch migrate from legacy enabled milestones, while a missing repeat interval safely becomes `None`.
