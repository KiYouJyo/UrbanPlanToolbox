[简体中文](RELEASE-NOTES-v1.9.4.md) | [日本語](RELEASE-NOTES-v1.9.4.ja.md) | English

# UrbanPlanToolbox v1.9.4 Theme Consistency Fix

- Locks the active navigation surface to Light `#E5F9F9` and Dark `#1A2323`, preventing Windows / Windows App SDK environment differences from changing the established teal shell theme.
- When the window deactivates, navigation chrome aligns with the system title bar on Light `#F3F3F3` / Dark `#202020` inactive surfaces and returns to teal when reactivated.
- Explicitly defines Active / Inactive × Light / Dark states while continuing to use system colors for High Contrast.
- Adds a regression contract for window activation state, event lifetime, theme changes, and inactive-surface selection to reduce machine-dependent UI drift.
- Project schema, backup format, and the GitHub / Microsoft Store updater mechanisms are unchanged.
