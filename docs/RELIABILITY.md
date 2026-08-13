# Reliability contract

## Startup

Prioritize creating and activating the main window. Settings, data, project, first-run, Mica/theme initialization, and window-state restoration must fail safely without blocking the shell or faking success. First-run work begins after the application can present its primary experience.

## Async operations

Operations use explicit `Idle`, `Running`, `Succeeded`, `Failed`, and `Canceled` outcomes. Prevent duplicate actions, restore UI in `finally`, distinguish cancellation from failure, and prevent disposed pages from updating controls.

## Updates

GitHub update states are `Checking`, `Downloading`, `Verifying`, `ReadyToInstall`, `Deploying`, `Restarting`, `Completed`, and `Failed`. Metadata must survive progress refreshes. An update is complete only after deployment and restart/new-version launch evidence, never merely after a completed download.

## Data

Use atomic save, last-valid recovery, future-schema refusal, migration rollback, and an import safety backup. Preserve valid data when validation, migration, or replacement fails.

## Logging and privacy

Log safe stages, result types, and HRESULTs only. Never log private keys, tokens, certificate material, complete user content, or sensitive local paths.

## Localization and release reliability

Maintain matching three-language resource key sets. Release validation covers Debug/Release x64, MSIX, installation, upgrade, language, theme, DPI, and channel identity as appropriate to the authorized release.

## Future engineering budgets

Establish measured baselines before enforcing cold-start, package-size, memory, tool-initialization, native-dependency inventory, and dependency-impact budgets. Do not invent values that have not been measured.
