English | [简体中文](StoreUpdateTesting.md) | [日本語](StoreUpdateTesting.ja.md)

# Store updater E2E

Refer to [project-status.json](project-status.json) for UrbanPlanToolbox state. The final baseline → v1.7.1 Store E2E is **PENDING**. Record discovery, localized 1.7.1 notes, download only, `ReadyToInstall`, no deployment or shutdown before the explicit Restart and update action, then Store deployment, v1.7.1 launch, and retained data. Per-package `Completed` callbacks must not advance the UI to a terminal state.
