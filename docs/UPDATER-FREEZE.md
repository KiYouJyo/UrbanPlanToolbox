# Updater freeze contract

The runtime updater is behavior-frozen for the validated GitHub path. `UpdateViewModel` is the application-scoped update session; `AboutPage` attaches on `Loaded`, detaches on `Unloaded`, and never owns an update cancellation token.

GitHub keeps its verified discovery, SHA-256, signature, deployment, and restart path. Microsoft Store uses one user action and `RequestDownloadAndInstallStorePackageUpdatesAsync`; Windows restart recovery is registered before that call. Per-package `Completed` is not a transaction terminal state. Navigation preserves checking, download progress, localized notes, target version, source, and retryable failure without starting a second Store operation.

The GitHub updater is validated and frozen. The v1.7.4 Store updater implementation is published and behavior-frozen. v1.7.5 is the final real Microsoft Store E2E validation target: it is freeze-ready / final-e2e-pending, not fully frozen, until a real Store 1.7.4 → Store 1.7.5 update proves native authorization, deployment, automatic relaunch, retry behavior, navigation continuity, and retained user data. Publication alone is not that E2E evidence.
