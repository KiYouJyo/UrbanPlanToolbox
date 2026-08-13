## 简体中文

UrbanPlanToolbox v1.5.6 正式发布。

**GitHub 安装与更新**

- 为 GitHub 分发版新增轻量一键安装方式，首次安装包不再内置完整应用程序，减少安装包体积。
- 修复部分 Windows 环境下通过 Windows App Installer 直接从 GitHub Release 获取应用包时可能安装失败的问题。
- 首次安装现在由安装程序从 GitHub Release 获取正式 MSIXBundle，并在本地完成部署。
- 安装前会验证下载文件的 SHA-256、程序包签名及发布者信息，避免安装损坏或不匹配的程序包。
- GitHub 应用内更新采用与首次安装一致的下载、验证和本地部署流程。
- 更新下载过程支持真实进度显示，并保持取消、失败恢复和诊断日志能力。

**分发与兼容性**

- GitHub 与 Microsoft Store 继续作为相互独立的安装和更新渠道。
- Microsoft Store 版本仍使用 Microsoft Store 更新机制，不受本次 GitHub 更新架构调整影响。
- GitHub 版本不再依赖 Windows App Installer 直接从 GitHub Release URL 远程部署程序包。
- 本版本未启用后台或启动时自动更新检查，更新仍由用户主动触发。
- 本版本不改变现有项目数据格式及主要工具功能。

---

## 日本語

UrbanPlanToolbox v1.5.6 を正式公開します。

**GitHub 版のインストールと更新**

- GitHub 配布版に軽量なワンクリックインストーラーを追加しました。初回インストールパッケージにはアプリ本体を含めないため、ダウンロードサイズを大幅に削減しています。
- 一部の Windows 環境で、Windows App Installer が GitHub Release からアプリパッケージを直接取得する際にインストールできない問題を修正しました。
- 初回インストールでは、正式な MSIXBundle を GitHub Release から取得し、ローカルで展開する方式に変更しました。
- インストール前に SHA-256、パッケージ署名、発行元情報を検証し、破損または不正なパッケージのインストールを防止します。
- GitHub 版のアプリ内更新も、初回インストールと同じダウンロード・検証・ローカル展開経路を使用します。
- 更新時には実際のダウンロード進捗を表示し、キャンセル、エラー処理、診断ログにも対応します。

**配布と互換性**

- GitHub と Microsoft Store は引き続き独立したインストール・更新チャネルとして動作します。
- Microsoft Store 版は従来どおり Microsoft Store の更新機構を使用し、今回の GitHub 更新方式の変更による影響はありません。
- GitHub 版では、Windows App Installer に GitHub Release URL から直接パッケージをリモート展開させる方式を使用しません。
- バックグラウンドまたは起動時の自動更新確認は有効にしておらず、更新確認はユーザー操作によって実行されます。
- 既存のプロジェクトデータ形式および主要機能に変更はありません。

---

## English

UrbanPlanToolbox v1.5.6 is now available.

**GitHub installation and updates**

- Added a lightweight one-click installer for the GitHub distribution. The first-install package no longer embeds the full application, significantly reducing its download size.
- Fixed an issue where installation could fail on some Windows environments when Windows App Installer attempted to obtain the application package directly from GitHub Releases.
- First-time installation now downloads the official MSIXBundle from GitHub Releases and performs deployment from the local package.
- SHA-256, package signature, and publisher information are verified before installation to prevent damaged or mismatched packages from being deployed.
- GitHub in-app updates use the same download, verification, and local deployment pipeline as first-time installation.
- Update downloads report real progress and retain cancellation, failure handling, and diagnostic logging.

**Distribution and compatibility**

- GitHub and Microsoft Store remain independent installation and update channels.
- Microsoft Store installations continue to use the Microsoft Store update mechanism and are unaffected by this GitHub update architecture change.
- GitHub installations no longer rely on Windows App Installer to deploy packages directly from a GitHub Release URL.
- Background and launch-time automatic update checks remain disabled; update checks are initiated by the user.
- Existing project data formats and primary tool functionality are unchanged.
