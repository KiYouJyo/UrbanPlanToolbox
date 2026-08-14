简体中文 | [日本語](RELEASE.ja.md) | [English](RELEASE.en.md)

# UrbanPlanToolbox 发布合同

## Authority

[project-status.json](project-status.json) is the repository authority for current product and channel status. [CHANGELOG.md](../CHANGELOG.md), versioned release notes, GitHub Releases, and Partner Center / Microsoft Store preserve historical and external facts. Historical release decisions are archived in [history/release-decisions-1.4-1.5.md](history/release-decisions-1.4-1.5.md).

## Release principles

A tag, GitHub Release, GitHub package, Store package, Store submission, Store certification, and Store publication are separate steps. Success at one step never proves success at another. A build, test, upload, or 100% download is not publication or a completed update.

## GitHub release contract

- Build from the final approved `main` commit with matching product and package versions, x64 target, GitHub sideload identity, and publisher.
- Produce the formal MSIXBundle, SHA-256 checksums, one-click bootstrap, and three-language release notes.
- Validate identity, publisher, package version, checksums, signatures, installation, and the GitHub updater end-to-end path before declaring success.

## GitHub updater E2E gate

Prove this sequence from a previous formal GitHub installation: **Checking → Downloading → Verifying → ReadyToInstall → Restart and update → new package registration → new-version launch → user-data retention**.

Also prove network, checksum, signature, publisher-mismatch, deployment, and interrupted-restart failures are explicit and recoverable. Download completion alone is not update success.

## Microsoft Store release contract

Store work requires explicit release authorization. Use the Store identity, `Package.Store.appxmanifest`, a valid package version, `msixupload`, Partner Center, and the required Store technical validation. Do not retain a fixed `x.0.0` / `x.5.0` rule. A Store submission is not a public release; only Partner Center and actual Store availability establish publication.

## Pre-release consistency

Before an authorized release, validate [project-status.json](project-status.json), `UrbanPlanToolbox.csproj`, both manifests, three-language release notes, website metadata, changelog, and channel metadata. Run the documentation consistency check as part of this review.

The release gate also requires all three Markdown sibling files, valid sibling links, complete structured `notes` for `zh-CN` / `ja-JP` / `en-US`, semantic equality between `Assets/Data/ReleaseNotes/X.Y.Z.json` and its GitHub Pages mirror, production-model deserialization, and a generated GitHub Release body. The generated body must use tag-pinned language URLs and contain no candidate-only publication status. Run `packaging/Sync-ReleaseNotes.ps1 -Version X.Y.Z -Check` and `packaging/New-GitHubReleaseBody.ps1 -Version X.Y.Z -OutputPath <path>` before tagging.

## Post-release state updates

After a confirmed GitHub publication, update the SSOT GitHub state to `published`. After a Store submission, use `certification-submitted`. Change Store state to `published` only with confirmation of actual public availability.

## Prohibitions

- Do not reuse a package from a different commit.
- Do not treat a GitHub package as a Store package.
- Do not describe certification submission as publication.
- Do not replace content of a published same-version package or upload different binaries under the same version.
- Do not commit test packages, local certificates, or credentials.
