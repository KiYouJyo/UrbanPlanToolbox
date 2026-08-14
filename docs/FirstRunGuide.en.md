English | [简体中文](FirstRunGuide.md) | [日本語](FirstRunGuide.ja.md)

# First-run guide

The UrbanPlanToolbox first-run guide follows [project-status.json](project-status.json). It preserves four steps, privacy, Skip/Back/Next, Escape, focus, and lifecycle behavior.

Automatic onboarding state is package-scoped in `first-run-guide.json`. The first-run guide is shown again after reinstall or Windows Reset. Retained projects, settings, and attachments are not treated as evidence that onboarding has been completed, and they are not deleted.

### Visual surface contract

The first-run guide does not maintain a separate Light/Dark color palette. Its outer surface reuses the main shell navigation-pane surface, and its content card reuses the standard application card surface. Theme behavior therefore remains owned by the application's shared Light, Dark, System, and High Contrast resources.
