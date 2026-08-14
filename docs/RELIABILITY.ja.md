日本語 | [简体中文](RELIABILITY.md) | [English](RELIABILITY.en.md)

# 信頼性契約

UrbanPlanToolbox の現在の事実は [project-status.json](project-status.json) に従います。

起動ではシェルの作成と有効化を優先し、失敗は安全に扱います。Store ネイティブの `Deploying` は `Installing` へ対応付け、Store の `Completed` は package deployment 完了を意味するため `RestartRequired` へ対応付けます。`Completed` はユーザー操作が残らない場合だけに使用します。GitHub は deployment 前の `ReadyToInstall`、Store は deployment 後の `RestartRequired` を内部状態として使いますが、ユーザー向けの流れは共通で、確認 → ダウンロードしてインストール → 再起動して更新です。Store updater の最終 v1.6.8 → v1.6.9 E2E は実際の Store 配信まで保留です。

ログには安全な段階、結果、HRESULT だけを記録し、トークン、鍵、個人データ、不要な絶対パスは記録しません。
