简体中文 | [日本語](RELIABILITY.ja.md) | [English](RELIABILITY.en.md)

# 可靠性合同

## Startup

Prioritize creating and activating the main window. Settings, data, project, first-run, Mica/theme initialization, and window-state restoration must fail safely without blocking the shell or faking success. First-run work begins after the application can present its primary experience.

## Async operations

Operations use explicit `Idle`, `Running`, `Succeeded`, `Failed`, and `Canceled` outcomes. Prevent duplicate actions, restore UI in `finally`, distinguish cancellation from failure, and prevent disposed pages from updating controls.

## Updates

The application update states include `ReadyToInstall` and `RestartRequired`. Microsoft Store follows Check → download only → `ReadyToInstall` → explicit user install action → deployment → new-version launch. A completed download is not a completed update, and a per-package progress callback is never an application-level terminal state; only the awaited Store operation's `OverallState` is authoritative. Store native `Deploying` maps to `Installing` only after the explicit install action. GitHub retains its independently verified download, deployment, and restart path. Store final E2E remains pending a real Store baseline-to-v1.7.1 delivery.

## Data

Use atomic save, last-valid recovery, future-schema refusal, migration rollback, and an import safety backup. Preserve valid data when validation, migration, or replacement fails.

## Logging and privacy

Log safe stages, result types, and HRESULTs only. Never log private keys, tokens, certificate material, complete user content, or sensitive local paths.

## Localization and release reliability

Maintain matching three-language resource key sets. Release validation covers Debug/Release x64, MSIX, installation, upgrade, language, theme, DPI, and channel identity as appropriate to the authorized release.

## Future engineering budgets

Establish measured baselines before enforcing cold-start, package-size, memory, tool-initialization, native-dependency inventory, and dependency-impact budgets. Do not invent values that have not been measured.
