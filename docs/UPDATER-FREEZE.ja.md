日本語 | [简体中文](UPDATER-FREEZE.md) | [English](UPDATER-FREEZE.en.md)

# Updater freeze 契約

GitHub と Microsoft Store の両方の runtime update 経路は、実際の配布環境での検証を完了し、凍結されました。`UpdateViewModel` は引き続きアプリケーション スコープの更新セッションを所有し、`AboutPage` は `Loaded` で接続、`Unloaded` で解除します。ページ自身は更新のキャンセル トークンを所有しません。

GitHub は検証済みの検出、SHA-256、署名、展開、再起動経路を維持します。Microsoft Store は、1 回のユーザー操作で `RequestDownloadAndInstallStorePackageUpdatesAsync` を呼び出す Windows ネイティブのダウンロード・インストール経路を維持し、その呼び出しより先に Windows の再起動回復を登録します。パッケージ単位の `Completed` は更新トランザクション全体の終端状態ではありません。画面遷移後も確認状態、ダウンロード進捗、ローカライズ済み更新内容、対象バージョン、更新元、再試行可能な失敗状態を保持し、2 つ目の Store 更新操作を開始してはなりません。

2026-08-14 に、実際の Microsoft Store **1.7.4 → 1.7.5** エンドツーエンド受け入れ検証が完了しました。これにより GitHub updater と Store updater はともに **validated / fully frozen** となり、従来の `final-e2e-pending` 状態は終了します。

凍結後は、機能追加、操作感の微調整、リファクタリングだけを理由として updater を変更しません。確認済みの updater 不具合、セキュリティ問題、または Windows / Microsoft Store のプラットフォーム・API 互換性要件がある場合に限り、このモジュールを再度開くことができます。その場合も、影響を受ける配布経路で完全な E2E 回帰証拠を再度そろえてから再凍結します。
