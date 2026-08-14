简体中文 | [日本語](RELIABILITY.ja.md) | [English](RELIABILITY.en.md)

# 可靠性合同

## Startup

Prioritize creating and activating the main window. Settings, data, project, first-run, Mica/theme initialization, and window-state restoration must fail safely without blocking the shell or faking success. First-run work begins after the application can present its primary experience.

## Async operations

Operations use explicit `Idle`, `Running`, `Succeeded`, `Failed`, and `Canceled` outcomes. Prevent duplicate actions, restore UI in `finally`, distinguish cancellation from failure, and prevent disposed pages from updating controls.

## Updates

The application update states include `ReadyToInstall` and `RestartRequired`. Store native `Deploying` maps to `Installing`; Store `Completed` means package deployment completed and maps to `RestartRequired`, never application-level `Completed`. `Completed` means no user action remains. The user-facing GitHub and Store flow is identical: Check → Update available → Download and install → Restart and update. Internally GitHub uses `ReadyToInstall` before deployment, while Store uses `RestartRequired` after deployment; both present the same final action. Store final E2E remains pending the real v1.6.8 → v1.6.9 Store path.

## Data

Use atomic save, last-valid recovery, future-schema refusal, migration rollback, and an import safety backup. Preserve valid data when validation, migration, or replacement fails.

## Logging and privacy

Log safe stages, result types, and HRESULTs only. Never log private keys, tokens, certificate material, complete user content, or sensitive local paths.

## Localization and release reliability

Maintain matching three-language resource key sets. Release validation covers Debug/Release x64, MSIX, installation, upgrade, language, theme, DPI, and channel identity as appropriate to the authorized release.

## Future engineering budgets

Establish measured baselines before enforcing cold-start, package-size, memory, tool-initialization, native-dependency inventory, and dependency-impact budgets. Do not invent values that have not been measured.
