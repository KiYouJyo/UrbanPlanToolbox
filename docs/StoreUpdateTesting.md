简体中文 | [日本語](StoreUpdateTesting.ja.md) | [English](StoreUpdateTesting.en.md)

# Microsoft Store 应用内更新 E2E 合同

Any change to Store update behavior requires end-to-end evidence; unit tests, a build, package creation, or a download indicator alone are insufficient.

## Test boundary

Use a formal Store installation at source product version **N** and a higher Store-delivered target product version **N+1**. A Package Flight may be used as the delivery mechanism, but neither a historical version nor a flight is the current release status authority.

## Required path

Prove: **existing Store installation → check for updates → available version → download only → ReadyToInstall without deployment → explicit user “Restart and update” action → Windows restart recovery registration → Store deployment → application close/restart behavior → target version → user-data retention**. Store deployment ownership is not relaunch ownership: a terminated process is relaunched by the registration; a surviving process must remove that registration before its `AppInstance.Restart` fallback.

## Required scenarios

- up to date;
- network failure;
- Store unavailable;
- download failure;
- installation failure;
- user cancellation; and
- retry after failure.

Capture the displayed state, package identity/version, deployment result, restart behavior, and retained user data. GitHub sideload packages and Store packages have independent identities and publishers, cannot upgrade over one another, and must be tested independently.

## v1.7.3 final Store E2E target

- Source: actual Microsoft Store baseline `N`
- Target: `1.7.3`
- Status: **PENDING**
- Prove: check → localized 1.7.3 notes → download only → `ReadyToInstall` → no deployment, restart, or process shutdown before the second action → explicit Restart and update → restart registration → installing/deployment → 1.7.3 launch → retained user data. Also prove cancellation returns to `ReadyToInstall` and a surviving process uses exactly one fallback restart. Per-package `Completed` callbacks must not advance the UI to a terminal state.
