English | [简体中文](RELIABILITY.md) | [日本語](RELIABILITY.ja.md)

# Reliability contract

Current UrbanPlanToolbox facts are governed by [project-status.json](project-status.json).

Startup prioritizes creation and activation of the shell and fails safely. UrbanPlanToolbox uses one main application instance per user session; secondary launch activations are redirected to that existing instance, whose minimized window is restored before activation. Microsoft Store uses Check → one explicit Download and install update action → register Windows restart recovery → Windows-native download and installation authorization → deployment → new-version launch. `RegisterApplicationRestart` is registered before the Store operation, so Windows relaunches an app terminated by deployment; if the Store operation returns while the process survives, the app removes that registration before its `AppInstance.Restart` fallback. Cancellation and failure remove the registration and return to `UpdateAvailable`. A per-package progress callback is not an application-level terminal state; only the awaited Store operation's `OverallState` is authoritative, and native Store `Deploying` maps to `Installing`. GitHub keeps its independent verified deployment and restart flow. Final Store v1.7.3 → v1.7.4 E2E is pending real Store delivery.

Logs record safe stages, result types, and HRESULTs only—never tokens, keys, user data, or unnecessary absolute paths.
