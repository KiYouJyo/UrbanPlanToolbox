English | [简体中文](StoreUpdateTesting.md) | [日本語](StoreUpdateTesting.ja.md)

# Store updater E2E

Refer to [project-status.json](project-status.json) for UrbanPlanToolbox state. Microsoft Store v1.7.4 is the **PUBLISHED** source, while v1.7.5 is the unpublished final real-device E2E validation target. The Store updater is **FINAL-E2E-PENDING / FREEZE-READY**, not fully frozen.

After publication, record Store 1.7.4 → discovery → localized 1.7.5 notes → one Download and install update action → restart recovery registration before the combined Store operation → native download/install authorization → Store deployment → automatic v1.7.5 launch → retained data. Verify cancellation returns to `UpdateAvailable`, retry works, leaving and returning to About does not start a second Store operation, and a surviving process unregisters recovery before exactly one app-owned fallback restart. Per-package `Completed` callbacks must not advance the UI to a terminal state.

Store publication confirms delivery status only; it does not by itself prove the real-device in-app updater or automatic relaunch path.
