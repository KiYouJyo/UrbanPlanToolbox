## 简体中文

UrbanPlanToolbox v1.6.5 主要修复 GitHub 侧载更新安装包的签名验证流程。

- 修复 MSIXBundle 下载完成后可能被错误判定为签名或 Publisher 不匹配的问题。
- 更新包现在通过 Windows 签名验证机制检查 Authenticode / MSIX 签名有效性。
- 进一步验证更新包的发布者 Subject 和正式发布证书 Thumbprint。
- 保留 SHA-256 完整性校验，并细化签名缺失、签名无效、发布者不匹配等失败状态。
- 增强更新验证阶段的诊断日志和安全性。
- GitHub 更新只接受由 UrbanPlanToolbox 当前正式发布证书签名的更新包。
- 校验同时检查发布者身份和证书 Thumbprint，避免只依赖 Subject 名称。

本版本仅通过 GitHub Releases 发布，Microsoft Store 不在本次发布范围内。

## 日本語

UrbanPlanToolbox v1.6.5 は、GitHub サイドロード版の更新パッケージ署名検証を主に修正しました。

- MSIXBundle のダウンロード完了後に、署名または Publisher の不一致として誤って拒否される問題を修正しました。
- Windows の署名検証機構を使用して、Authenticode / MSIX 署名の有効性を確認するようにしました。
- 更新パッケージの Publisher Subject に加え、正式リリース証明書の Thumbprint も検証します。
- SHA-256 による整合性検証を維持し、署名なし、署名無効、Publisher 不一致などの失敗状態をより明確にしました。
- 更新パッケージ検証時の診断ログと安全性を改善しました。
- GitHub 更新では、UrbanPlanToolbox の現在の正式リリース証明書で署名された更新パッケージのみを受け入れます。
- Publisher 名だけでなく証明書 Thumbprint も固定して確認します。

このバージョンは GitHub Releases のみで公開します。Microsoft Store は今回のリリース対象外です。

## English

UrbanPlanToolbox v1.6.5 primarily fixes signature verification for GitHub sideload update packages.

- Fixed an issue where a downloaded MSIXBundle could be incorrectly rejected as having a mismatched signature or publisher.
- Update packages are now validated using Windows signature verification for Authenticode / MSIX signatures.
- The updater verifies both the publisher subject and the thumbprint of the production release certificate.
- SHA-256 integrity verification remains in place, with clearer failure states for missing signatures, invalid signatures, and signer mismatches.
- Improved diagnostics and security during update-package verification.
- GitHub updates accept only packages signed with the current UrbanPlanToolbox production release certificate.
- Both publisher identity and certificate thumbprint are pinned instead of relying on the subject name alone.

This version is released through GitHub Releases only. Microsoft Store is not part of this release.
