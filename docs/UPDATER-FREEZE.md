# Updater freeze contract

The runtime updater is behavior-frozen after v1.7.1 validation. `UpdateViewModel` is the application-scoped update session; `AboutPage` attaches on `Loaded`, detaches on `Unloaded`, and never owns an update cancellation token.

GitHub keeps its verified discovery, SHA-256, signature, deployment, and restart path. Store uses one user action and `RequestDownloadAndInstallStorePackageUpdatesAsync`; Windows restart recovery is registered before that call. Per-package `Completed` is not a transaction terminal state. Navigation preserves checking, download progress, localized notes, target version, source, and retryable failure without starting a second Store operation.

The GitHub updater is validated and frozen. The Store updater is freeze-ready, not fully frozen, until a real Store baseline N to v1.7.1 update proves the same navigation behavior.
