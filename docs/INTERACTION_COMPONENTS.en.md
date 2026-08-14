English | [简体中文](INTERACTION_COMPONENTS.md) | [日本語](INTERACTION_COMPONENTS.ja.md)

# Interaction components

The current UrbanPlanToolbox design contract follows [project-status.json](project-status.json). Reuse shared components without breaking established card, focus, or input-routing behavior.

## Transient surface contract

App-owned ContentDialog, ComboBox dropdowns, and Flyouts do not maintain a separate Light/Dark palette. They reuse shared theme surfaces, borders, text, and interaction states; business pages provide content and behavior only.

Transient UI customizes theme surfaces only and never replaces WinUI default control templates or geometry. The ContentDialog body remains opaque; ComboBox dropdowns use the app-level transient surface while preserving default rounded geometry, animation, selection, and keyboard behavior.

ComboBox dropdown surfaces consume the application's shared transient surface through WinUI's native `ComboBoxDropDownBackground` theme resource while retaining the default ComboBox template, geometry, animation, and item interaction states.
