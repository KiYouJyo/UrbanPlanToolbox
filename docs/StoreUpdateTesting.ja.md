日本語 | [简体中文](StoreUpdateTesting.md) | [English](StoreUpdateTesting.en.md)

# Store updater E2E

UrbanPlanToolbox の状態は [project-status.json](project-status.json) を参照します。baseline から v1.7.3 への最終 E2E は **PENDING** です。更新確認、三言語の 1.7.3 更新内容、ダウンロードのみ、`ReadyToInstall`、明示的な「再起動して更新」操作前に deployment または終了がないこと、Windows 再起動回復の登録、その後の Store deployment、v1.7.3 起動、データ保持を記録します。キャンセルで `ReadyToInstall` に戻ること、およびプロセスが残る場合に回復登録を解除してから 1 回だけアプリ側のフォールバック再起動を実行することを確認します。パッケージ単位の `Completed` callback が UI を終端状態へ進めてはなりません。
