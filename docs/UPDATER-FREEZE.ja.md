# Updater freeze 契約

検証済みの GitHub 更新経路について runtime updater の動作を凍結します。`UpdateViewModel` はアプリケーション スコープの更新セッションを所有し、`AboutPage` は `Loaded` で接続、`Unloaded` で解除します。ページは更新のキャンセル トークンを所有しません。

GitHub は検証済みの検出、SHA-256、署名、展開、再起動経路を維持します。Microsoft Store は 1 回のユーザー操作で `RequestDownloadAndInstallStorePackageUpdatesAsync` を実行し、その呼び出しより先に Windows の再起動回復を登録します。パッケージ単位の `Completed` はトランザクションの終端状態ではありません。画面遷移後も確認状態、ダウンロード進捗、ローカライズ済み更新内容、対象バージョン、更新元、再試行可能な失敗状態を保持し、2 つ目の Store 操作を開始しません。

GitHub updater は検証済みで凍結されています。v1.7.4 Store updater 実装は公開済みで動作を凍結しています。v1.7.5 は最終の Microsoft Store 実機 E2E 検証対象です。実際の Store 1.7.4 → Store 1.7.5 更新でネイティブ承認、展開、自動再起動、再試行、画面遷移の継続性、ユーザーデータ保持を確認するまで、状態は freeze-ready / final-e2e-pending であり、fully frozen ではありません。公開完了だけでは E2E の証拠になりません。
