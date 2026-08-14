日本語 | [简体中文](RELIABILITY.md) | [English](RELIABILITY.en.md)

# 信頼性契約

UrbanPlanToolbox の現在の事実は [project-status.json](project-status.json) に従います。

起動ではシェルの作成と有効化を優先し、失敗は安全に扱います。UrbanPlanToolbox はユーザーセッションごとに 1 つのメインアプリインスタンスを使用し、後から起動されたアクティブ化は既存インスタンスへリダイレクトされ、最小化中のウィンドウはアクティブ化前に復元されます。Microsoft Store は、確認 → 「アップデートをダウンロードしてインストール」→ Windows 再起動回復の登録 → Windows ネイティブのダウンロード・インストール承認 → 展開 → 新バージョン起動の順で処理します。`RegisterApplicationRestart` は Store 操作より先に登録されるため、展開で終了したアプリは Windows が再起動します。Store 操作が戻ってプロセスが残る場合は、登録を解除してから `AppInstance.Restart` をフォールバックとして使用します。キャンセルと失敗では登録を解除して `UpdateAvailable` に戻ります。パッケージ単位の progress callback はアプリ全体の終端状態ではなく、await された Store operation の `OverallState` だけが確定結果です。ネイティブ Store の `Deploying` は `Installing` へ対応付けます。GitHub は検証済みの独立した展開・再起動フローを維持します。v1.7.4 Store 実装は公開済みで動作を凍結しています。実機での Store 1.7.4 → 1.7.5 E2E は final-e2e-pending であり、公開成功だけをもって E2E 成功とはみなしません。

ログには安全な段階、結果、HRESULT だけを記録し、トークン、鍵、個人データ、不要な絶対パスは記録しません。
