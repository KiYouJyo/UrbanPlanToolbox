English | [简体中文](RELIABILITY.md) | [日本語](RELIABILITY.ja.md)

# Reliability contract

Current UrbanPlanToolbox facts are governed by [project-status.json](project-status.json).

Startup prioritizes creation and activation of the shell and fails safely. Native Store `Deploying` maps to `Installing`; Store `Completed` means package deployment completed and maps to `RestartRequired`, not application-level `Completed`. `Completed` is reserved for an update with no remaining user action. GitHub uses `ReadyToInstall` before deployment and Store uses `RestartRequired` after deployment, while both expose Check → Download and install → Restart and update. Final Store v1.6.8 → v1.6.9 E2E is pending real Store delivery.

Logs record safe stages, result types, and HRESULTs only—never tokens, keys, user data, or unnecessary absolute paths.
