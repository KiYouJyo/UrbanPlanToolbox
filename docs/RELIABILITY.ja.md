日本語 | [简体中文](RELIABILITY.md) | [English](RELIABILITY.en.md)

# 信頼性契約

UrbanPlanToolbox の現在の事実は [project-status.json](project-status.json) に従います。

起動ではシェルの作成と有効化を優先し、失敗は安全に扱います。Microsoft Store は、確認 → ダウンロードのみ → `ReadyToInstall` → ユーザーによる明示的なインストール操作 → 展開 → 新バージョン起動の順で処理します。ダウンロード完了は更新完了ではなく、パッケージ単位の progress callback もアプリ全体の終端状態ではありません。await された Store operation の `OverallState` だけが確定結果です。ネイティブ Store の `Deploying` は明示的なインストール後にのみ `Installing` へ対応付けます。GitHub は検証済みの独立した展開・再起動フローを維持します。Store baseline から v1.7.1 への最終 E2E は実配信まで保留です。

ログには安全な段階、結果、HRESULT だけを記録し、トークン、鍵、個人データ、不要な絶対パスは記録しません。
