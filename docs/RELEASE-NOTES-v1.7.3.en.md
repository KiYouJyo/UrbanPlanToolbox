English | [简体中文](RELEASE-NOTES-v1.7.3.md) | [日本語](RELEASE-NOTES-v1.7.3.ja.md)

# UrbanPlanToolbox v1.7.3 Microsoft Store update relaunch fix

- Fixes the app not automatically relaunching after a Microsoft Store update closes it.
- Registers Windows restart recovery before Store deployment; if the operation returns while the old process survives, an app-owned restart completes the version switch.
- Improves recovery and retry behavior for cancellation, installation failures, and restart failures.
- Preserves the Store download-then-explicit-install flow, GitHub updates, and single-instance behavior.
