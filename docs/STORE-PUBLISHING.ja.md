日本語 | [简体中文](STORE-PUBLISHING.md) | [English](STORE-PUBLISHING.en.md)

# Microsoft Store 発行契約

現在の Store 状態は [project-status.json](project-status.json) で定義します。公開状態の最終的な根拠は Partner Center と実際の Microsoft Store の可用性であり、リポジトリ内メタデータや送信コマンドの成功だけから公開済みと判断してはいけません。

## Store ID とパッケージ

- Store ID: `9MWDPJG1BHKW`
- Package identity: `JoKiy.UrbanPlanToolbox`
- Publisher: `CN=C4E4B33A-7B77-4121-897C-7D720A5471F8`
- Package family name: `JoKiy.UrbanPlanToolbox_4wdwgytaw3v2m`
- Manifest: `Package.Store.appxmanifest`
- Distribution channel: `Store`

Store manifest と Store package は Store チャネルだけで使用します。GitHub サイドロード版の identity、publisher、署名チェーン、package、updater は独立しており、チャネル間の上書き更新には使用できません。

## WinGet / Microsoft Store ソース

Windows Package Manager の既定 `msstore` ソースは Microsoft Store カタログを使用します。UrbanPlanToolbox の Store product ID は `9MWDPJG1BHKW` なので、Store 版は次のコマンドでインストールできます。

```powershell
winget install --id 9MWDPJG1BHKW --source msstore -e
```

これは第三の package identity ではなく、独立した WinGet 発行 workflow も不要です。`msstore` からインストールしたものは Microsoft Store 版であり、その後の更新も Store が管理します。利用可否は実際の Store カタログ状態に従います。

現在の GitHub サイドロード `.msixbundle` を WinGet Community Repository に直接送信してはいけません。この package はプロジェクトの自己署名証明書を使用し、初回インストール bootstrap が証明書の信頼設定を行う前提です。WinGet Community 経路はこの信頼設定を代行せず、スクリプト型 bootstrap も Community installer として受け付けられません。将来、クリーンなシステムで追加 bootstrap なしに信頼され、サイレントインストールできる installer を提供できた場合にのみ、別の `winget` Community package を再検討します。

## Authorized workflow

Store submission の workflow entry point は `.github/workflows/publish-microsoft-store.yml` です。各 Store submission には明示的な承認が必要で、Partner Center に接続する前に source commit、version alignment、release notes、package identity、publisher、resources、package evidence を検証します。

## Submission lifecycle

Store package を準備・検証したうえで、明示的な承認がある場合だけ送信します。submission、certification、public availability は別状態として扱います。送信済みは `certification-submitted`、Partner Center と実際の Store availability で公開を確認した場合のみ `published` とします。

## Failure recovery

状態不明の pending submission を上書き・削除してはいけません。先に Partner Center の状態を読み、submission と package evidence を保持したまま正確な状態を診断してから再試行します。certification、publication、upload の失敗や unknown state は成功扱いにしません。

## WACK と secrets

必要な場合は、最終的に承認された package に対して WACK / Store technical validation を実行します。証拠がない状態で合格したと記録してはいけません。Store credentials は承認済み secrets にのみ保存し、証明書、秘密鍵、client secret、token、ローカル package、diagnostic export をコミットしてはいけません。

## Version と channel rules

承認済みリリースの前に product version と両 manifest の package version を release metadata と一致させます。Store package version は Partner Center に対して有効かつ単調増加でなければなりません。Store submission は GitHub release より低頻度でもよく、GitHub release から自動的に Store submission を推論してはいけません。WinGet `msstore` の可用性は Store catalog に従い、独立した release-state authority ではありません。
