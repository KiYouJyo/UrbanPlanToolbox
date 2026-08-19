[简体中文](RELEASE-NOTES-v1.9.1.md) | [日本語](RELEASE-NOTES-v1.9.1.ja.md) | English

# UrbanPlanToolbox v1.9.1 Engineering closeout and version governance

- Aligns application, assembly, GitHub/Store manifest, candidate release metadata, and project-status SSOT versions, eliminating the split between the 1.9.0 build state and stale 1.8.5 documentation state.
- Strengthens CI documentation consistency checks so project-status product and candidate package versions must match `release/release.json`, the project file, and both manifests. Future version-source drift is detected before merge.
- Replaces the hard-coded 1.9.0 signed-acceptance path with a version-agnostic flow driven by current release metadata, removing the need to clone an acceptance workflow for each maintenance version.
- Backfills CHANGELOG history for 1.8.1 through 1.9.0 and adds trilingual plus structured runtime release notes for 1.9.1.
- This closeout does not change project schemas, the backup format, updater behavior, or user-facing features. v1.9.1 is currently a validation candidate only; it does not automatically create a GitHub Release or submit to Microsoft Store.
