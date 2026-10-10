[简体中文](RELEASE-NOTES-v1.9.4.md) | 日本語 | [English](RELEASE-NOTES-v1.9.4.en.md)

# UrbanPlanToolbox v1.9.4 テーマ表示整合性修正

- アクティブ時のナビゲーション表面を Light `#E5F9F9` / Dark `#1A2323` に固定し、Windows / Windows App SDK 環境差によって既存の青系テーマが変化する問題を抑えます。
- ウィンドウが非アクティブになると、ナビゲーション領域をシステムタイトルバーと揃えて Light `#F3F3F3` / Dark `#202020` へ切り替え、再アクティブ化で青系表面へ戻します。
- Active / Inactive × Light / Dark の4状態を明示し、High Contrast は引き続きシステムカラーリソースを使用します。
- ウィンドウのアクティブ状態に関する回帰契約を追加し、イベント購読解除、テーマ変更、非アクティブ表面の選択を検証します。
- Project Schema、バックアップ形式、GitHub / Microsoft Store の更新機構は変更しません。
