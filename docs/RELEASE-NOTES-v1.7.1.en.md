English | [简体中文](RELEASE-NOTES-v1.7.1.md) | [日本語](RELEASE-NOTES-v1.7.1.ja.md)

# UrbanPlanToolbox v1.7.1 Store update flow fix

- Fixes Microsoft Store updates showing Restart and update before download completion and implements a true download-then-install flow.
- Store package deployment no longer begins until the user chooses Restart and update.
- Fixes treating Store package-progress Completed as the whole update transaction and strengthens multi-package and asynchronous-callback state-machine coverage.
- Keeps the GitHub update flow and application-scoped update-session behavior unchanged.
