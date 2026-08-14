日本語 | [简体中文](RELIABILITY.md) | [English](RELIABILITY.en.md)

# 信頼性契約

UrbanPlanToolbox の現在の事実は [project-status.json](project-status.json) に従います。

起動ではシェルの作成と有効化を優先し、失敗は安全に扱います。更新のアプリ状態は `Installing` を使用し、Store ネイティブの `Deploying` はこの状態へ対応付けます。GitHub updater は検証済みで凍結されています。Store updater の最終 E2E は実際の Store 配信まで保留です。

ログには安全な段階、結果、HRESULT だけを記録し、トークン、鍵、個人データ、不要な絶対パスは記録しません。
