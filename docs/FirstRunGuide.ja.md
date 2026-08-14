日本語 | [简体中文](FirstRunGuide.md) | [English](FirstRunGuide.en.md)

# 初回起動ガイド

UrbanPlanToolbox の初回起動ガイドは [project-status.json](project-status.json) の契約に従います。4 ステップ、プライバシー、Skip/Back/Next、Escape、フォーカス、ライフサイクルを保持します。

初回起動の状態は package-scoped LocalState の `first-run-guide.json` が唯一の基準です。アンインストール後の再インストール、または Windows のリセット後は、初回起動ガイドが再度表示されます。保持されているプロジェクト・設定・添付ファイルは、ガイド完了済みの判定には使用せず、削除もしません。

### ビジュアル Surface 契約

初回起動ガイドは独自の Light / Dark 背景色を管理しません。外側の背景はメインアプリのナビゲーションペインと同じテーマ Surface を共有し、中央のコンテンツカードは通常のアプリ内カードと同じテーマ Surface を共有します。
