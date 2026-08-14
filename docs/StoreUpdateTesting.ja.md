日本語 | [简体中文](StoreUpdateTesting.md) | [English](StoreUpdateTesting.en.md)

# Store updater E2E

UrbanPlanToolbox の状態は [project-status.json](project-status.json) を参照します。Microsoft Store v1.7.4 は **PUBLISHED** のソースであり、v1.7.5 は未公開の最終実機 E2E 検証対象です。Store updater の状態は **FINAL-E2E-PENDING / FREEZE-READY** であり、fully frozen ではありません。

公開後に、Store 1.7.4 → 更新確認 → 三言語の 1.7.5 更新内容 →「アップデートをダウンロードしてインストール」の 1 回の操作 → 結合された Store 操作より先の再起動回復登録 → ネイティブのダウンロード・インストール承認 → Store deployment → 自動 v1.7.5 起動 → データ保持を記録します。キャンセルで `UpdateAvailable` に戻ること、再試行、About ページから離れて戻っても 2 つ目の Store 操作を開始しないこと、およびプロセスが残る場合に回復登録を解除してから 1 回だけアプリ側のフォールバック再起動を実行することを確認します。パッケージ単位の `Completed` callback が UI を終端状態へ進めてはなりません。

Store の公開完了は配信状態の確認であり、それだけでは実機のアプリ内 updater や自動再起動経路の成功を証明しません。
