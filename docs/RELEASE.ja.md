日本語 | [简体中文](RELEASE.md) | [English](RELEASE.en.md)

# リリース契約

[project-status.json](project-status.json) は UrbanPlanToolbox の候補版と実際の配布状態を分離して記録します。

タグ、GitHub Release、アセット、Store パッケージ、Store 送信、認証、公開は別のゲートです。ビルド、テスト、アップロード、ダウンロード完了は公開または更新完了の証明ではありません。

リリース前には、三つの Markdown sibling files、sibling links、`zh-CN` / `ja-JP` / `en-US` の完全な構造化 `notes`、`Assets/Data/ReleaseNotes/X.Y.Z.json` と GitHub Pages mirror の semantic equality、production model の deserialization、生成済み GitHub Release body を確認します。body は tag 固定の言語 URL を使用し、候補専用の公開状態を含めません。tag 作成前に `packaging/Sync-ReleaseNotes.ps1 -Version X.Y.Z -Check` と `packaging/New-GitHubReleaseBody.ps1 -Version X.Y.Z -OutputPath <path>` を実行します。
