简体中文 | [日本語](RELEASE.ja.md) | [English](RELEASE.en.md)

# UrbanPlanToolbox 发布合同

## Authority

[project-status.json](project-status.json) is the repository authority for current product and channel status. [CHANGELOG.md](../CHANGELOG.md), versioned release notes, GitHub Releases, and Partner Center / Microsoft Store preserve historical and external facts. Historical release decisions are archived in [history/release-decisions-1.4-1.5.md](history/release-decisions-1.4-1.5.md).

## Release principles

A tag, GitHub Release, GitHub package, Store package, Store submission, Store certification, and Store publication are separate steps. Success at one step never proves success at another. A build, test, upload, or 100% download is not publication or a completed update.

## 发布授权与单 PR 规则

当维护者已经明确要求某个版本“直接发布”或“执行到双端发布完成”时，**开发 PR 本身就是唯一的发布审批点**。该 PR 应在 `release/release.json` 中直接设置目标版本，并将 `channels.github.publish` 与 `channels.microsoftStore.submit` 设置为 `true`。开发 PR 合并到 `main` 后，release orchestrator 直接创建不可变 tag，并启动 GitHub Release 与 Microsoft Store 工作流。

**禁止再为同一版本创建只把 `release-candidate` 改成 `release-approved`、或只把 publish/submit 从 `false` 改成 `true` 的纯批准 PR。** `classification.stability == release-approved` 不再是发布编排器的强制门槛；历史记录中可以继续保留该状态，但未来发布不依赖它。

如果任务明确要求“只开发、暂不发布”，则开发 PR 保持两个 channel flag 为 `false`。之后需要发布现有 candidate 时，使用 release orchestrator 的手动 `workflow_dispatch`，提交当前版本号并输入精确确认文本 `PUBLISH X.Y.Z`；不再通过额外 approval-only PR 获得授权。

## GitHub release contract

- Build from the final authorized `main` commit with matching product and package versions, x64 target, GitHub sideload identity, and publisher.
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

## 发布流程完结条件

必须先推送发布后的状态提交，并且该提交对应的必需 `main` CI 已 `completed` 且为 `success`，才能宣布发布流程完全收尾。queued、in-progress 或 waiting 的 CI 都属于中间状态；CI 失败时必须进行发布后 CI 修复。

## Prohibitions

- Do not reuse a package from a different commit.
- Do not treat a GitHub package as a Store package.
- Do not describe certification submission as publication.
- Do not replace content of a published same-version package or upload different binaries under the same version.
- Do not commit test packages, local certificates, or credentials.
