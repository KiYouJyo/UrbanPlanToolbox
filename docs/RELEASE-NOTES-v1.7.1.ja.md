日本語 | [简体中文](RELEASE-NOTES-v1.7.1.md) | [English](RELEASE-NOTES-v1.7.1.en.md)

# UrbanPlanToolbox v1.7.1 Store 更新フローの修正

- Microsoft Store 更新でダウンロード完了前に「再起動して更新」が表示される問題を修正し、ダウンロード後にインストールする二段階フローへ変更しました。
- ユーザーが「再起動して更新」を選ぶまで、Store パッケージの展開を開始しません。
- Store package progress の Completed を更新全体の完了として扱う問題を修正し、複数パッケージと非同期コールバックの状態機械テストを強化しました。
- GitHub 更新フローとアプリケーション全体の更新セッション動作を維持します。
