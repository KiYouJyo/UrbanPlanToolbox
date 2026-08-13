## 简体中文

UrbanPlanToolbox v1.5.4 正式发布。

**修复与改进**

- 修复发现新版本时更新确认弹窗标题可能显示本地化资源键的问题。
- 重构 Microsoft Store 应用内更新的进度回调链路，提高真实下载进度获取的可靠性。
- 下载阶段优先使用 Microsoft Store 提供的总体下载进度，并增加 package 进度和实际下载字节数回退机制。
- 修复无有效进度的回调可能导致已显示下载进度被重新清零的问题。
- 改进安装部署阶段状态、更新生命周期和 Microsoft Store 诊断日志。
- 完善简体中文、日语和英语更新界面资源处理。

本版本不改变现有项目数据格式及主要工具功能。Microsoft Store 真实更新链路仍需未来通过 v1.5.4 → v1.5.5 验证。

## 日本語

UrbanPlanToolbox v1.5.4 を正式公開します。

**修正と改善**

- 更新確認ダイアログのタイトルにローカライズ用リソースキーが表示される問題を修正しました。
- Microsoft Store アプリ内更新の進捗コールバック処理を見直し、実際のダウンロード進捗取得の信頼性を向上しました。
- ダウンロード中は Store の総ダウンロード進捗を優先し、パッケージ進捗と実際のダウンロード済みバイト数をフォールバックとして使用します。
- 有効な進捗値を含まないコールバックによって表示済みの進捗がリセットされる問題を修正しました。
- インストール状態、更新ライフサイクル、Microsoft Store 診断ログを改善しました。
- 中国語（簡体字）、日本語、英語の更新 UI リソースを整備しました。

既存のプロジェクトデータ形式と主要機能は変更していません。Microsoft Store の実更新経路は、今後 v1.5.4 → v1.5.5 で検証します。

## English

UrbanPlanToolbox v1.5.4 is now available.

**Fixes and improvements**

- Fixed an issue where the update confirmation dialog could display a localization resource key instead of its title.
- Revised the Microsoft Store in-app update progress callback pipeline for more reliable real download progress reporting.
- Download progress now prioritizes the overall Store-reported progress, with package-progress and downloaded-byte fallbacks.
- Fixed callbacks without a valid progress value resetting an already displayed download progress.
- Improved installation-state handling, update lifecycle behavior, and Microsoft Store diagnostics.
- Improved Simplified Chinese, Japanese, and English update-interface resources.

Existing project data formats and primary tool functionality are unchanged. The real Microsoft Store update path remains pending validation through v1.5.4 → v1.5.5.
