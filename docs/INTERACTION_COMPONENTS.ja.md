日本語 | [简体中文](INTERACTION_COMPONENTS.md) | [English](INTERACTION_COMPONENTS.en.md)

# インタラクションコンポーネント

UrbanPlanToolbox の現行設計契約は [project-status.json](project-status.json) に従います。既存のカード、フォーカス、入力ルーティングを壊さずに共有コンポーネントを使用します。

## Transient Surface 契約

アプリ内の ContentDialog、ComboBox ドロップダウン、Flyout は独自の Light/Dark パレットを管理せず、共有テーマ Surface、境界線、文字、操作状態を再利用します。業務ページは内容と操作だけを提供します。

一時的な Surface はテーマ Surface だけを設定し、WinUI の既定テンプレートやジオメトリを置き換えません。ContentDialog 本体は不透明であり、ComboBox ドロップダウンは既定の角丸、アニメーション、選択、キーボード操作を維持します。

ComboBox のドロップダウン Surface は、WinUI 標準の `ComboBoxDropDownBackground` テーマリソースを通じてアプリ共通の transient Surface を使用し、既定のテンプレート、角丸、アニメーション、項目状態を維持します。
