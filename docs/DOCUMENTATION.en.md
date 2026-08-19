English | [简体中文](DOCUMENTATION.md) | [日本語](DOCUMENTATION.ja.md)

# Documentation governance

UrbanPlanToolbox supports `zh-CN`, `ja-JP`, and `en-US` as formal documentation languages. Current canonical documents use sibling files: `.md`, `.ja.md`, and `.en.md`.

[project-status.json](project-status.json) is the SSOT for current product, candidate, and channel state. Machine-readable JSON keys are not translated; human-facing explanation is trilingual. Historical evidence, third-party licenses, and legal source text are not retroactively translated.

Release Notes have two presentations. `RELEASE-NOTES-vX.Y.Z.md`, `.ja.md`, and `.en.md` are the human-readable Markdown sibling files and the only editable Markdown source. `Assets/Data/ReleaseNotes/X.Y.Z.json` is the sole editable runtime structured source; `docs/release-notes/X.Y.Z.json` is its GitHub Pages mirror, synchronized by `packaging/Sync-ReleaseNotes.ps1`. Both use the same `notes`, locale, title, and items schema. A GitHub Release body is a publication representation generated from the Chinese Markdown with tag-pinned sibling URLs, not a fourth independent Release Notes source.

The current governance stage is not aimed at adding new product features. Maintenance versions advance to validation and explicit publication approval only after version SSOT, Release Notes, CHANGELOG, and release metadata consistency has been verified.
