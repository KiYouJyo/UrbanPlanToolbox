简体中文 | [日本語](STORE-PUBLISHING.ja.md) | [English](STORE-PUBLISHING.en.md)

# Microsoft Store 发布合同

Current Store status is defined in [project-status.json](project-status.json). The final authorities for public availability are Partner Center and the Microsoft Store client or product page; do not infer publication from `update-manifest.json`, a GitHub Release body, or a successful submission command.

## Store identity and package

- Store ID: `9MWDPJG1BHKW`
- Package identity: `JoKiy.UrbanPlanToolbox`
- Publisher: `CN=C4E4B33A-7B77-4121-897C-7D720A5471F8`
- Package family name: `JoKiy.UrbanPlanToolbox_4wdwgytaw3v2m`
- Manifest: `Package.Store.appxmanifest`
- Distribution channel: `Store`

Use the Store manifest and Store package only for the Store channel. GitHub sideload identity, publisher, signing chain, package, and updater are independent and cannot provide cross-channel upgrades.

## WinGet / Microsoft Store source

Windows Package Manager 的默认 `msstore` 源直接使用 Microsoft Store 目录。UrbanPlanToolbox 的 Store 产品 ID 为 `9MWDPJG1BHKW`，因此 Store 版可通过以下命令安装：

```powershell
winget install --id 9MWDPJG1BHKW --source msstore -e
```

这不是第三套发行包，也不需要独立的 WinGet 发布工作流。通过 `msstore` 安装的仍是 Microsoft Store 身份，后续更新继续由 Store 管理，其可用性取决于实际 Store 上架状态。

不要把当前 GitHub 旁加载 `.msixbundle` 直接提交到 WinGet Community Repository：GitHub 包使用项目自签名证书，并依赖首次安装 bootstrap 配置信任；WinGet Community 不会替应用完成这一步，且脚本型 bootstrap 不能作为 Community installer。只有在未来提供可由干净系统直接信任和静默安装的独立安装器后，才评估额外的 `winget` Community source 包。

## Authorized workflow

The workflow entry point is `.github/workflows/publish-microsoft-store.yml` and must be explicitly authorized for each Store submission. It verifies the approved source commit, version alignment, release notes, package identity, publisher, resources, and package evidence before interacting with Partner Center.

## Submission lifecycle

Prepare and validate the Store package, then submit only with explicit authorization. Treat submission, certification, and public availability as different states. Record a submission as `certification-submitted`; use `published` only after Partner Center and Store availability confirm public release.

## Failure recovery

Do not overwrite or delete an unknown pending submission. Read Partner Center first, preserve submission and package evidence, and diagnose the exact state before retrying. Failed certification, publication, upload, or unknown states are failures, not successful publication.

## WACK and secrets

Run WACK / Store technical validation for the final authorized package when required. Do not claim it passed without recorded evidence. Store credentials belong in approved secrets only; never commit certificates, private keys, client secrets, tokens, local packages, or diagnostic exports.

## Version and channel rules

The product version and both manifest package versions must align with the release metadata before an authorized release. Store package versions must be valid and monotonic relative to Partner Center. Store submissions can be less frequent than GitHub releases and are never implied by a GitHub release. WinGet `msstore` availability follows the Store catalog and does not create a separate release-state authority.
