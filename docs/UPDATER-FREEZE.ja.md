# Updater freeze 契約

v1.7.0 検証後、runtime updater の動作を凍結します。`UpdateViewModel` はアプリケーション スコープの更新セッションであり、`AboutPage` は `Loaded` で接続し `Unloaded` で解除します。ページは更新のキャンセル トークンを所有しません。

GitHub の検出、SHA-256、署名、展開、再起動の検証済み経路を維持します。Store の `Completed` は常に `RestartRequired` に対応付けます。実 Store の N から v1.7.0 への更新が完了するまで Store は freeze-ready です。
