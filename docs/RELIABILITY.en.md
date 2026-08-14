English | [简体中文](RELIABILITY.md) | [日本語](RELIABILITY.ja.md)

# Reliability contract

Current UrbanPlanToolbox facts are governed by [project-status.json](project-status.json).

Startup prioritizes creation and activation of the shell and fails safely. Microsoft Store uses Check → download only → `ReadyToInstall` → explicit user install action → deployment → new-version launch. A completed download is not a completed update, and a per-package progress callback is not an application-level terminal state; only the awaited Store operation's `OverallState` is authoritative. Native Store `Deploying` maps to `Installing` only after explicit installation. GitHub keeps its independent verified deployment and restart flow. Final Store baseline → v1.7.1 E2E is pending real Store delivery.

Logs record safe stages, result types, and HRESULTs only—never tokens, keys, user data, or unnecessary absolute paths.
