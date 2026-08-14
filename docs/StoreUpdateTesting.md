简体中文 | [日本語](StoreUpdateTesting.ja.md) | [English](StoreUpdateTesting.en.md)

# Microsoft Store 应用内更新 E2E 合同

Any change to Store update behavior requires end-to-end evidence; unit tests, a build, package creation, or a download indicator alone are insufficient.

## Test boundary

Use a formal Store installation at source product version **N** and a higher Store-delivered target product version **N+1**. A Package Flight may be used as the delivery mechanism, but neither a historical version nor a flight is the current release status authority.

## Required path

Prove: **existing Store installation → check for updates → available version → download state → Store deployment → application close/restart behavior → target version → user-data retention**.

## Required scenarios

- up to date;
- network failure;
- Store unavailable;
- download failure;
- installation failure;
- user cancellation; and
- retry after failure.

Capture the displayed state, package identity/version, deployment result, restart behavior, and retained user data. GitHub sideload packages and Store packages have independent identities and publishers, cannot upgrade over one another, and must be tested independently.

## v1.6.9 final Store E2E target

- Source: actual Microsoft Store `1.6.8`
- Target: `1.6.9`
- Status: **PENDING**
- Prove: check → localized 1.6.9 notes → download → installing → Store `Completed` → `RestartRequired` → Restart and update → v1.6.9 launch → retained user data.
