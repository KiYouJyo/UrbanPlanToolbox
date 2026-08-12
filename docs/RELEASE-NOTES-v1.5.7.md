# UrbanPlanToolbox v1.5.7

## 简体中文

UrbanPlanToolbox v1.5.7 正式发布。

### 更新体验

- 统一 GitHub 与 Microsoft Store 的更新检查体验。
- 发现新版本时显示与应用主题一致的本地化确认弹窗。
- 更新管理页面不再直接显示完整 GitHub Release 正文。
- 移除不可靠的下载进度条与百分比，仅显示当前更新状态。
- 修复更新管理中当前版本可能显示远端版本的问题。
- 修复 About 页面版权符号编码错误。

### 分发与兼容性

- GitHub 与 Microsoft Store 继续使用独立的下载和安装机制。
- GitHub 安装继续从 GitHub Releases 获取并验证 MSIXBundle 后进行本地部署。
- Microsoft Store 安装继续使用 Microsoft Store 更新机制。
- 不启用后台或启动时自动更新检查，更新检查仍由用户主动触发。
- 不改变现有项目数据格式。

---

## 日本語

UrbanPlanToolbox v1.5.7 を正式に公開しました。

### 更新体験

- GitHub 版と Microsoft Store 版の更新確認 UI を統一しました。
- 新しいバージョンがある場合、現在のアプリ言語によるテーマ対応の確認ダイアログを表示します。
- 更新管理画面に GitHub Release 本文を直接表示しなくなりました。
- 信頼できないダウンロード進捗率とプログレスバーを削除し、現在の状態だけを表示します。
- 更新管理画面で現在のバージョンがリモート版として表示される問題を修正しました。
- About ページの著作権記号の文字化けを修正しました。

### 配布と互換性

- GitHub と Microsoft Store は独立したダウンロード・インストール経路を引き続き使用します。
- GitHub 版は GitHub Releases から MSIXBundle を取得・検証してローカルに展開します。
- Microsoft Store 版は Microsoft Store の更新機構を使用します。
- バックグラウンドまたは起動時の自動更新確認は有効にしていません。
- 既存のプロジェクトデータ形式は変更していません。

---

## English

UrbanPlanToolbox v1.5.7 is now available.

### Update experience

- Unified the update-check experience for GitHub and Microsoft Store installations.
- Added a themed, localized confirmation dialog when an update is available.
- Removed the complete GitHub Release body from the Update section.
- Removed unreliable progress bars and percentages and kept state-only feedback.
- Fixed the Update section showing a remote version instead of the currently installed version.
- Fixed the copyright-symbol encoding issue on the About page.

### Distribution and compatibility

- GitHub and Microsoft Store continue to use independent download and installation pipelines.
- GitHub installations continue to obtain and verify the official MSIXBundle from GitHub Releases before local deployment.
- Microsoft Store installations continue to use the Microsoft Store update mechanism.
- Background and launch-time automatic update checks remain disabled.
- Existing project data formats are unchanged.
