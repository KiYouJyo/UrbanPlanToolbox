English | [简体中文](RELIABILITY.md) | [日本語](RELIABILITY.ja.md)

# Reliability contract

Current UrbanPlanToolbox facts are governed by [project-status.json](project-status.json).

Startup prioritizes creation and activation of the shell and fails safely. The app update contract uses `Installing`; native Store `Deploying` maps to it. The GitHub updater is validated and frozen. Final Store updater E2E is pending real Store delivery.

Logs record safe stages, result types, and HRESULTs only—never tokens, keys, user data, or unnecessary absolute paths.
