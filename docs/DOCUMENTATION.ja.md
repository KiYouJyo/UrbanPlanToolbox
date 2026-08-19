日本語 | [简体中文](DOCUMENTATION.md) | [English](DOCUMENTATION.en.md)

# ドキュメント統治

UrbanPlanToolbox は `zh-CN`、`ja-JP`、`en-US` を正式な文書言語として扱います。現在の正式文書は同名の sibling files（`.md`、`.ja.md`、`.en.md`）で維持します。

[project-status.json](project-status.json) は現在の製品・候補・チャネル状態の SSOT です。機械可読 JSON のキーは翻訳せず、人向けの説明は三言語で提供します。履歴の証跡、第三者ライセンス、法的原文には遡及翻訳を要求しません。

Release Notes は二つの presentation を持ちます。`RELEASE-NOTES-vX.Y.Z.md`、`.ja.md`、`.en.md` は人が読む Markdown の sibling files であり、唯一の Markdown 編集元です。`Assets/Data/ReleaseNotes/X.Y.Z.json` はアプリ実行時の構造化した唯一の編集元で、`docs/release-notes/X.Y.Z.json` は `packaging/Sync-ReleaseNotes.ps1` による GitHub Pages mirror です。両者は `notes`、locale、title、items の同一 schema を維持します。GitHub Release body は中国語 Markdown から tag 固定 sibling URL 用に生成する publication representation であり、第四の source ではありません。

現在の統治段階は新しい製品機能の追加を目的としません。保守版は、バージョン SSOT、Release Notes、CHANGELOG、リリースメタデータの整合性を確認した後に検証と明示的な公開承認へ進みます。
