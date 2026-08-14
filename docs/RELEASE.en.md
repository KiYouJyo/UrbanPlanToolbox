English | [简体中文](RELEASE.md) | [日本語](RELEASE.ja.md)

# Release contract

[project-status.json](project-status.json) separates the UrbanPlanToolbox source candidate from actual distribution state.

A tag, GitHub Release, asset upload, Store package, Store submission, certification, and publication are separate gates. A build, test, upload, or completed download proves neither publication nor a completed update.

Before tagging, verify the three Markdown sibling files, their sibling links, complete structured `notes` for `zh-CN` / `ja-JP` / `en-US`, semantic equality between `Assets/Data/ReleaseNotes/X.Y.Z.json` and its GitHub Pages mirror, production-model deserialization, and a generated GitHub Release body. The body must use tag-pinned language URLs and contain no candidate-only publication status. Run `packaging/Sync-ReleaseNotes.ps1 -Version X.Y.Z -Check` and `packaging/New-GitHubReleaseBody.ps1 -Version X.Y.Z -OutputPath <path>`.
