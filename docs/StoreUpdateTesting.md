简体中文 | [日本語](StoreUpdateTesting.ja.md) | [English](StoreUpdateTesting.en.md)

# Microsoft Store 应用内更新 E2E 合同

Any change to Store update behavior requires end-to-end evidence; unit tests, a build, package creation, publication, or a download indicator alone are insufficient.

## Test boundary

Use a formal Store installation at source product version **N** and the Microsoft Store-published target product version **1.7.4**. A Package Flight may be used as an auxiliary delivery mechanism, but neither a historical version nor a flight replaces the current release-status authority.

## Required path

Prove: **existing Store installation → check for updates → available version → user selects “下载并安装更新” → Windows restart recovery registration → native Store download authorization → native Store installation authorization → deployment → automatic application relaunch → target version → user-data retention**. Store deployment ownership is not relaunch ownership: a terminated process is relaunched by the registration; a surviving process must remove that registration before its `AppInstance.Restart` fallback.

## Required scenarios

- up to date;
- network failure;
- Store unavailable;
- download failure;
- installation failure;
- user cancellation; and
- retry after failure.

Capture the displayed state, package identity/version, deployment result, restart behavior, and retained user data. GitHub sideload packages and Store packages have independent identities and publishers, cannot upgrade over one another, and must be tested independently.

## v1.7.4 final Store E2E target

- Source: actual Microsoft Store baseline `N`
- Target: published Microsoft Store version `1.7.4`
- Publication status: **PUBLISHED**
- Updater E2E status: **PENDING**
- Prove: check → localized 1.7.4 notes → one Download and install update action → restart registration before the combined Store operation → native download/install authorization → deployment → automatic 1.7.4 launch → retained user data. Also prove cancellation returns to `UpdateAvailable`, a surviving process uses exactly one fallback restart, and package-level `Completed` callbacks do not advance the UI to a terminal state.

Microsoft Store publication confirms delivery status only; it does not by itself prove the real-device in-app updater or automatic relaunch path.
