[简体中文](RELEASE-NOTES-v1.8.1.md) | [日本語](RELEASE-NOTES-v1.8.1.ja.md) | English

# UrbanPlanToolbox v1.8.1 Background Residency, Tray Menu, and Project Workspace Fixes

- Fixes an issue where the resident background process could block MSIX uninstall; the resident process now exits when uninstall of the current app package begins.
- Replaces the system tray right-click menu with a compact WinUI 3-style popup. The menu can now open while both the main window and Inspiration Recorder are hidden, and it remains above the Windows hidden-icons flyout.
- Hides the notification-area icon while the main UrbanPlanToolbox window is visible in the taskbar, then restores the tray icon when the main window returns to background residency.
- Fixes Microsoft Store update checks showing a previous version number or previous release notes before download. The UI now displays only a trustworthy target version that is strictly newer than the installed version and only release notes that match that target.
- Streamlines design project workspaces by hiding administrative area and latitude/longitude fields, moving the project description into Basic information, removing the planning requirements card, and moving Save and Reset to the bottom of the page.
- Moves Save and Reset to the bottom of research project workspaces as well.
- Preserves data compatibility for hidden legacy design fields so existing administrative area, coordinates, and planning requirements are not cleared when older projects are saved.