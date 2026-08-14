日本語 | [简体中文](RELEASE-NOTES-v1.7.3.md) | [English](RELEASE-NOTES-v1.7.3.en.md)

# UrbanPlanToolbox v1.7.3 Microsoft Store 更新後の再起動修正

- Microsoft Store の更新インストール後にアプリが閉じても自動的に再起動しない問題を修正しました。
- Store 展開前に Windows の再起動回復を登録し、処理が戻って旧プロセスが残る場合はアプリ側の再起動で切り替えます。
- キャンセル、インストール失敗、再起動失敗時の復帰と再試行を改善しました。
- ダウンロード後に明示的にインストールする Store 更新、GitHub 更新、単一インスタンスの動作を維持します。
