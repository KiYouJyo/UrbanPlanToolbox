English | [简体中文](StoreUpdateTesting.md) | [日本語](StoreUpdateTesting.ja.md)

# Store updater E2E

Any change to Store update behavior requires real end-to-end evidence. Unit tests, builds, package creation, certification submission, publication, or a download indicator alone do not replace real-device in-app update acceptance.

## Completed final acceptance

- Source: Microsoft Store production **1.7.4**
- Target: Microsoft Store production **1.7.5**
- Acceptance date: **2026-08-14**
- Store publication status: **PUBLISHED**
- Updater E2E status: **PASSED / FULLY FROZEN**

The real Store 1.7.4 → 1.7.5 update acceptance has completed, closing the previous `FINAL-E2E-PENDING / FREEZE-READY` state. This real E2E is the final evidence used to mark the Microsoft Store updater fully frozen; Store publication alone is not treated as substitute evidence.

## Frozen path

The Microsoft Store update path is fixed as: **existing Store installation → check for updates → available version and localized notes → one Download and install update action → Windows restart recovery registration before the Store operation → Windows-native download and installation authorization → Store deployment → automatic new-version launch → retained user data**.

Store deployment may terminate the old process. When it does, the pre-registered Windows restart recovery owns relaunch. If the Store operation returns while the old process survives, the app must unregister recovery first and then use exactly one `AppInstance.Restart` fallback. Per-package `Completed` callbacks are not application-level terminal states; only the awaited Store operation's `OverallState` is authoritative. Cancellation or failure must restore a retryable state, and leaving and returning to the page must not start a second Store operation.

## Reopening criteria

After the freeze, the Store updater is not changed for feature expansion, interaction polish, or internal refactoring alone. It may be reopened only for a confirmed updater defect, security issue, or Windows / Microsoft Store platform or API compatibility requirement. Any change requires a new real E2E on the affected channel before the updater can be marked frozen again.
