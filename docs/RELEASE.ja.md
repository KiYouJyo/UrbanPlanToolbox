日本語 | [简体中文](RELEASE.md) | [English](RELEASE.en.md)

# リリース契約

[project-status.json](project-status.json) は UrbanPlanToolbox の候補版と実際の配布状態を分離して記録します。

タグ、GitHub Release、アセット、Store パッケージ、Store 送信、認証、公開は別のゲートです。ビルド、テスト、アップロード、ダウンロード完了は公開または更新完了の証明ではありません。

## リリース承認と単一 PR ルール

メンテナーがあるバージョンについて「そのまま公開する」または「GitHub / Microsoft Store の両方まで完了する」と明示した場合、開発 PR 自体を唯一のリリース承認点とします。その PR で `release/release.json` の対象バージョンを設定し、`channels.github.publish` と `channels.microsoftStore.submit` の両方を `true` にします。開発 PR が `main` にマージされた後、release orchestrator が不変の tag を作成し、GitHub Release と Microsoft Store のワークフローを起動します。

`release-candidate` を `release-approved` に変えるだけ、または publish / submit を `false` から `true` に変えるだけの追加 approval-only PR を作成してはいけません。`classification.stability == release-approved` はリリース編成の必須ゲートではなくなりました。履歴上のメタデータとして残っていても構いませんが、今後の公開はそれに依存しません。

「開発のみで、まだ公開しない」と明示された場合は、両方の channel flag を `false` のままにします。その既存 candidate を後から公開する場合は、release orchestrator を手動で `workflow_dispatch` し、現在のバージョンと正確な確認文字列 `PUBLISH X.Y.Z` を入力します。追加の approval-only PR は作成しません。

リリース前には、三つの Markdown sibling files、sibling links、`zh-CN` / `ja-JP` / `en-US` の完全な構造化 `notes`、`Assets/Data/ReleaseNotes/X.Y.Z.json` と GitHub Pages mirror の semantic equality、production model の deserialization、生成済み GitHub Release body を確認します。body は tag 固定の言語 URL を使用し、候補専用の公開状態を含めません。tag 作成前に `packaging/Sync-ReleaseNotes.ps1 -Version X.Y.Z -Check` と `packaging/New-GitHubReleaseBody.ps1 -Version X.Y.Z -OutputPath <path>` を実行します。

## リリース完了条件

公開後の状態コミットが push 済みであり、そのコミットの必須 `main` CI が `completed` かつ `success` になって初めて、リリース工程を完全完了と宣言できます。queued、in-progress、waiting の CI は中間状態です。CI が失敗した場合は、公開後 CI 修復が必要です。
