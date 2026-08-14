English | [简体中文](RELEASE.md) | [日本語](RELEASE.ja.md)

# Release contract

[project-status.json](project-status.json) separates the UrbanPlanToolbox source candidate from actual distribution state.

A tag, GitHub Release, asset upload, Store package, Store submission, certification, and publication are separate gates. A build, test, upload, or completed download proves neither publication nor a completed update.

## Release authorization and single-PR rule

When the maintainer explicitly requests a version to be published or completed through both distribution channels, the development PR itself is the only release approval point. That PR must set the target version in `release/release.json` and set both `channels.github.publish` and `channels.microsoftStore.submit` to `true`. After that development PR merges to `main`, the release orchestrator creates the immutable tag and dispatches the GitHub Release and Microsoft Store workflows.

Do not create a second approval-only PR whose only purpose is to change `release-candidate` to `release-approved` or flip the publish/submit flags from `false` to `true`. `classification.stability == release-approved` is no longer a required orchestration gate; it may remain in historical metadata but future publication does not depend on it.

If a task explicitly requests development without publication, keep both channel flags `false`. To publish that existing candidate later, manually dispatch the release orchestrator with the current version and the exact confirmation text `PUBLISH X.Y.Z`; do not create an approval-only PR.

Before tagging, verify the three Markdown sibling files, their sibling links, complete structured `notes` for `zh-CN` / `ja-JP` / `en-US`, semantic equality between `Assets/Data/ReleaseNotes/X.Y.Z.json` and its GitHub Pages mirror, production-model deserialization, and a generated GitHub Release body. The body must use tag-pinned language URLs and contain no candidate-only publication status. Run `packaging/Sync-ReleaseNotes.ps1 -Version X.Y.Z -Check` and `packaging/New-GitHubReleaseBody.ps1 -Version X.Y.Z -OutputPath <path>`.

## Release terminal condition

The post-publication status commit must be pushed and its required `main` CI must be `completed` with `success` before the release process can be declared fully complete. Queued, in-progress, or waiting CI is an intermediate state; a failed CI requires post-release CI repair.
