English | [简体中文](RELEASE-NOTES-v1.7.4.md) | [日本語](RELEASE-NOTES-v1.7.4.ja.md)

# UrbanPlanToolbox v1.7.4 Microsoft Store update flow improvements

- Restores Microsoft Store updates to the official Windows combined download-and-install flow, avoiding system authorization at the wrong point in a two-stage update.
- After choosing Download and install update, Windows / Microsoft Store handles download and installation authorization in sequence. Windows restart recovery is registered first so the new version relaunches automatically after the app closes.
- If the old process survives a completed Store update, the app-level restart remains the fallback. GitHub updates, single-instance behavior, and other features are unchanged.
