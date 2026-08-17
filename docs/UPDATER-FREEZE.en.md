English | [简体中文](UPDATER-FREEZE.md) | [日本語](UPDATER-FREEZE.ja.md)

# Updater freeze contract

Both the GitHub and Microsoft Store runtime update paths are now validated in real distribution environments and frozen. `UpdateViewModel` remains the application-scoped update session; `AboutPage` attaches on `Loaded`, detaches on `Unloaded`, and owns no update cancellation token.

GitHub retains its verified discovery, SHA-256, signature, deployment, and restart path. Microsoft Store retains the Windows-native download-and-install flow in which one user action invokes `RequestDownloadAndInstallStorePackageUpdatesAsync`, with Windows restart recovery registered before that call. Per-package `Completed` is not a transaction terminal state. Navigation must preserve checking, download progress, localized notes, target version, source, and retryable failure without starting a second Store operation.

The real Microsoft Store **1.7.4 → 1.7.5** end-to-end acceptance completed on 2026-08-14. Both the GitHub updater and Store updater are therefore **validated / fully frozen**, and the previous `final-e2e-pending` state is closed.

**v1.8.3** on 2026-08-17 is a controlled exception for confirmed presentation defects: it keeps update fields visible while trusted Store target metadata is unresolved and revises the responsive update card, the single checking-button ProgressRing, and release-note scrollbar spacing. It does not change the GitHub download, verification, deployment, or restart path and does not change the Microsoft Store `RequestDownloadAndInstallStorePackageUpdatesAsync` transaction, so the underlying updater mechanisms remain frozen.

After this freeze, updater changes are not permitted merely for feature expansion, interaction polish, or refactoring. The module may be reopened only for a confirmed updater defect, security issue, or Windows / Microsoft Store platform or API compatibility requirement. Any such change must provide complete end-to-end regression evidence on the affected channel before the updater can be frozen again.
