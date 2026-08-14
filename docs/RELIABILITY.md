简体中文 | [日本語](RELIABILITY.ja.md) | [English](RELIABILITY.en.md)

# 可靠性合同

## Startup

Prioritize creating and activating the main window. UrbanPlanToolbox uses one main application instance per user session; secondary launch activations are redirected to the existing instance, whose minimized window is restored before activation. Settings, data, project, first-run, Mica/theme initialization, and window-state restoration must fail safely without blocking the shell or faking success. First-run work begins after the application can present its primary experience.

## Async operations

Operations use explicit `Idle`, `Running`, `Succeeded`, `Failed`, and `Canceled` outcomes. Prevent duplicate actions, restore UI in `finally`, distinguish cancellation from failure, and prevent disposed pages from updating controls.

## Updates

The application update states include `ReadyToInstall` and `RestartRequired` for the GitHub path. Microsoft Store follows Check → one explicit Download and install update action → register Windows restart recovery → Windows-native download and installation authorization → deployment → new-version launch. Store deployment may terminate the process; when it does, Windows owns relaunch through `RegisterApplicationRestart`, which is registered before the Store operation. If the Store operation returns while the process survives, the registration is removed before the app-owned `AppInstance.Restart` fallback. Cancellation and failure remove the registration and restore `UpdateAvailable` so the user can retry. A per-package progress callback is never an application-level terminal state; only the awaited Store operation's `OverallState` is authoritative, and native `Deploying` maps to `Installing`. GitHub retains its independently verified download, deployment, and restart path. The v1.7.4 Store implementation is published and behavior-frozen; the final real-device Store 1.7.4 → 1.7.5 E2E remains final-e2e-pending and must not be inferred from publication success alone.

## Data

Use atomic save, last-valid recovery, future-schema refusal, migration rollback, and an import safety backup. Preserve valid data when validation, migration, or replacement fails.

## Logging and privacy

Log safe stages, result types, and HRESULTs only. Never log private keys, tokens, certificate material, complete user content, or sensitive local paths.

## Localization and release reliability

Maintain matching three-language resource key sets. Release validation covers Debug/Release x64, MSIX, installation, upgrade, language, theme, DPI, and channel identity as appropriate to the authorized release.

## Future engineering budgets

Establish measured baselines before enforcing cold-start, package-size, memory, tool-initialization, native-dependency inventory, and dependency-impact budgets. Do not invent values that have not been measured.
