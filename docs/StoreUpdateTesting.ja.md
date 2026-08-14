日本語 | [简体中文](StoreUpdateTesting.md) | [English](StoreUpdateTesting.en.md)

# Store updater E2E

Store の更新動作を変更する場合は、実際のエンドツーエンド証拠が必要です。単体テスト、ビルド、パッケージ作成、認定への提出、公開、またはダウンロード表示だけでは、実機のアプリ内更新受け入れを代替できません。

## 完了した最終受け入れ

- ソース：Microsoft Store 正式版 **1.7.4**
- ターゲット：Microsoft Store 正式版 **1.7.5**
- 受け入れ日：**2026-08-14**
- Store 公開状態：**PUBLISHED**
- Updater E2E 状態：**PASSED / FULLY FROZEN**

実際の Store 1.7.4 → 1.7.5 更新受け入れが完了し、従来の `FINAL-E2E-PENDING / FREEZE-READY` 状態は終了しました。この実機 E2E を Microsoft Store updater を fully frozen とする最終証拠とし、Store の公開成功だけを代替証拠とはみなしません。

## 凍結後の固定経路

Microsoft Store の更新経路は、**既存の Store インストール → 更新確認 → 利用可能バージョンとローカライズ済み更新内容 → 「アップデートをダウンロードしてインストール」の 1 回の操作 → Store 操作より先に Windows restart recovery を登録 → Windows ネイティブのダウンロード・インストール承認 → Store deployment → 新バージョンの自動起動 → ユーザーデータ保持** に固定します。

Store deployment により旧プロセスが終了する場合は、事前登録した Windows restart recovery が再起動を担当します。Store 操作が戻った時点で旧プロセスが残っている場合は、アプリが先に recovery registration を解除し、その後に 1 回だけ `AppInstance.Restart` をフォールバックとして使用します。パッケージ単位の `Completed` callback はアプリ全体の終端状態ではなく、await された Store operation の `OverallState` だけが確定結果です。キャンセルまたは失敗後は再試行可能な状態へ戻し、ページを離れて戻っても 2 つ目の Store 操作を開始してはなりません。

## 再度開く条件

凍結後は、機能追加、操作感の微調整、内部リファクタリングだけを理由に Store updater を変更しません。確認済みの updater 不具合、セキュリティ問題、または Windows / Microsoft Store のプラットフォーム・API 互換性要件がある場合に限り変更できます。変更した場合は、影響を受ける配布経路で新たな実機 E2E を完了してから再び frozen とします。
