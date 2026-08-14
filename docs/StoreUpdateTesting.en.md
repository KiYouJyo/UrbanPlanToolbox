English | [简体中文](StoreUpdateTesting.md) | [日本語](StoreUpdateTesting.ja.md)

# Store updater E2E

Refer to [project-status.json](project-status.json) for UrbanPlanToolbox state. The final baseline → v1.7.4 Store E2E is **PENDING**. Record discovery, localized 1.7.4 notes, one Download and install update action, restart recovery registration before the combined Store operation, native download/install authorization, Store deployment, automatic v1.7.4 launch, and retained data. Verify cancellation returns to `UpdateAvailable`, and a surviving process unregisters recovery before exactly one app-owned fallback restart. Per-package `Completed` callbacks must not advance the UI to a terminal state.
