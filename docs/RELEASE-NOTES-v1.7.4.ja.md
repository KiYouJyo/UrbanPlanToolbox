日本語 | [简体中文](RELEASE-NOTES-v1.7.4.md) | [English](RELEASE-NOTES-v1.7.4.en.md)

# UrbanPlanToolbox v1.7.4 Microsoft Store 更新フローの改善

- Microsoft Store の更新を Windows 公式の一体型ダウンロード・インストール フローに戻し、2 段階更新でシステム承認画面が不自然なタイミングに表示される問題を回避します。
- 「アップデートをダウンロードしてインストール」を選ぶと、Windows / Microsoft Store がダウンロードとインストールの承認を順に処理します。開始前に Windows のアプリ再起動回復を登録し、アプリ終了後に新しいバージョンを自動起動します。
- Store 更新後も古いプロセスが残る場合はアプリ側の再起動をフォールバックとして使用します。GitHub 更新、単一インスタンス機構、その他の機能は変更されません。
