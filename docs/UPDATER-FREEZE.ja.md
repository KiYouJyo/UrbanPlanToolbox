日本語 | [简体中文](UPDATER-FREEZE.md) | [English](UPDATER-FREEZE.en.md)

# Updater freeze 契約

GitHub と Microsoft Store の両方の runtime update 経路は、実際の配布環境での検証を完了し、凍結されています。`UpdateViewModel` はアプリケーション スコープの更新セッションを所有し、`AboutPage` は `Loaded` で接続、`Unloaded` で解除します。

GitHub は検証済みの検出、SHA-256、署名、展開、再起動経路を維持します。Microsoft Store は、1 回のユーザー操作で `RequestDownloadAndInstallStorePackageUpdatesAsync` を呼び出す Windows ネイティブのダウンロード・インストール経路を維持します。画面遷移後も確認状態、ダウンロード進捗、ローカライズ済み更新内容、対象バージョン、更新元、再試行可能な失敗状態を保持します。

2026-08-14 に Microsoft Store **1.7.4 → 1.7.5** の実環境 E2E 受け入れ検証が完了し、GitHub updater と Store updater はともに **validated / fully frozen** となりました。

2026-08-17 の **v1.8.3** は確認済みの表示不具合に対する管理された例外で、更新カードの表示のみを修正しました。GitHub のダウンロード・検証・展開・再起動経路、および Microsoft Store の `RequestDownloadAndInstallStorePackageUpdatesAsync` トランザクションは変更していません。

その後の **v1.8.4** と **v1.8.5** も凍結済み機構を維持しています。v1.8.5 は設定／バージョン情報画面のレイアウトと、ウィンドウ非アクティブ時のシェル配色のみを調整し、更新管理のダウンロード、検証、インストール、再起動、Store トランザクションのロジックは変更していません。

凍結後は、確認済みの updater 不具合、セキュリティ問題、または Windows / Microsoft Store のプラットフォーム・API 互換性要件がある場合に限り、このモジュールを再度開きます。その場合は、影響を受ける配布経路で完全な E2E 回帰証拠を再度そろえてから再凍結します。
