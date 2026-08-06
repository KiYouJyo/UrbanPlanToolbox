# First-run guide architecture

The first-run guide is a reusable in-window host, not a second window or a native splash-screen replacement. `MainWindow` creates one `FirstRunGuideHost` over the existing shell. Closing the host leaves the existing page, navigation selection, title bar, backdrop, and activation flow intact.

`FirstRunExperienceService` stores only machine-local lifecycle state in `first-run-guide.json` under the app data root. It is separate from `settings.json`, project data, backups, imports, exports, and restore-defaults operations. `CurrentFirstRunGuideVersion` is currently `1`; later guide revisions can increment this version without coupling the guide to the product version.

On the first state read, an existing settings file, data directory, or managed attachments directory identifies an installation that predates the guide, so that installation is marked complete and is not forced through onboarding. An empty app-data root remains eligible for automatic onboarding. State read or write failures are non-fatal; the shell can still start and a failed completion remains retryable.

The guide is shown automatically after the first frame, infrastructure initialization, and reminder refresh. Completion and skip both persist the current guide version. Closing the app or pressing Escape does not persist completion, so an interrupted automatic guide is offered again. Manual reopening from the Application settings card uses the same host and never resets lifecycle state.

The three resource catalogs contain the same guide key set. Dynamic text is resolved through `LocalizationService`, so reopening after a language switch uses the active language. Privacy is opened through the packaged `PRIVACY.md` document; the guide adds no networking, telemetry, project-data examples, or consent state.
