[简体中文](RELEASE-NOTES-v1.8.1.md) | [日本語](RELEASE-NOTES-v1.8.1.ja.md) | English

# UrbanPlanToolbox v1.8.1 Background Uninstall, Tray Menu, and Project Workspace Cleanup

- Fixes an issue where the resident background process could block MSIX uninstall; the resident process now exits when uninstall of the current app package begins.
- Replaces the system tray right-click menu with a compact WinUI 3-style popup while preserving Open, Inspiration Recorder, Settings, and Exit actions.
- Streamlines design project workspaces by hiding administrative area and latitude/longitude fields, moving the project description into Basic information, removing the planning requirements card, and moving Save and Reset to the bottom of the page.
- Moves Save and Reset to the bottom of research project workspaces as well.
- Preserves data compatibility for hidden legacy design fields so existing administrative area, coordinates, and planning requirements are not cleared when older projects are saved.