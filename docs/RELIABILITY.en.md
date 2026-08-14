English | [简体中文](RELIABILITY.md) | [日本語](RELIABILITY.ja.md)

# Reliability contract

Current UrbanPlanToolbox facts are governed by [project-status.json](project-status.json).

Startup prioritizes creation and activation of the shell and fails safely. UrbanPlanToolbox uses one main application instance per user session; secondary launch activations are redirected to that existing instance, whose minimized window is restored before activation. Microsoft Store uses Check → one explicit Download and install update action → register Windows restart recovery → Windows-native download and installation authorization → deployment → new-version launch. `RegisterApplicationRestart` is registered before the Store operation, so Windows relaunches an app terminated by deployment; if the Store operation returns while the process survives, the app removes that registration before its `AppInstance.Restart` fallback. Cancellation and failure remove the registration and return to `UpdateAvailable`. A per-package progress callback is not an application-level terminal state; only the awaited Store operation's `OverallState` is authoritative, and native Store `Deploying` maps to `Installing`. GitHub keeps its independent verified deployment and restart flow. The v1.7.4 Store implementation is published and behavior-frozen; the final real-device Store 1.7.4 → 1.7.5 E2E remains final-e2e-pending and must not be inferred from publication success alone.

Logs record safe stages, result types, and HRESULTs only—never tokens, keys, user data, or unnecessary absolute paths.
