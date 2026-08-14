English | [简体中文](RELEASE-NOTES-v1.7.2.md) | [日本語](RELEASE-NOTES-v1.7.2.ja.md)

# UrbanPlanToolbox v1.7.2 single-instance activation

- UrbanPlanToolbox now runs as a single main application instance by default.
- Launching again while the app is open brings the existing window forward instead of creating another toolbox window.
- A minimized main window is restored and activated when the app is launched again.
- Improves Windows App SDK lifecycle and activation redirection while preserving Microsoft Store and GitHub update restart compatibility.
