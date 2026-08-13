## 简体中文

- 新增「调研照片整理器」，用于规划、设计与 GIS 实地调研照片整理。
- 支持批量选择和拖放导入 JPG、JPEG、HEIC、HEIF、PNG 照片，并提供缩略图预览。
- 读取照片 EXIF、GPS、拍摄时间、海拔和方向信息。
- 使用自由输入的标签与备注整理照片，并导出统一命名的照片副本。
- 导出 WGS 84 / EPSG:4326 Shapefile 点位和 CSV 元数据；无 GPS 照片保留在照片和 CSV 输出中，但不进入点图层，也不生成假坐标。
- 原始照片保持不变；照片和 GPS 仅在本机处理。HEIC 预览可能依赖 Windows 图像编解码器，但不影响已支持的元数据读取。
- 工具入口：设计工具 → 实地调研 → 调研照片整理器。

## 日本語

- 「調査写真整理ツール」を追加しました。都市・地域計画、設計、GIS の現地調査写真を整理できます。
- JPG、JPEG、HEIC、HEIF、PNG の一括選択とドラッグ＆ドロップによる読み込み、サムネイル表示に対応します。
- 写真の EXIF、GPS、撮影時刻、標高、方位を読み取ります。
- 自由入力のタグとメモで写真を整理し、統一した名前の写真コピーを出力できます。
- WGS 84 / EPSG:4326 の Shapefile ポイントと CSV メタデータを出力します。GPS のない写真は写真と CSV に残しますが、ポイントレイヤーには含めず、仮の座標も生成しません。
- 元写真は変更せず、写真と GPS はローカルで処理します。HEIC のプレビューは Windows の画像コーデックに依存する場合がありますが、対応するメタデータの読み取りには影響しません。
- ツール入口：設計ツール → 現地調査 → 調査写真整理ツール。

## English

- Added Survey Photo Organizer for planning, design, and GIS field-survey photos.
- Supports batch selection and drag-and-drop import of JPG, JPEG, HEIC, HEIF, and PNG photos with thumbnail preview.
- Reads EXIF, GPS, capture time, altitude, and heading metadata.
- Organizes photos with free-form Tags and Note fields, then exports consistently named photo copies.
- Exports WGS 84 / EPSG:4326 Shapefile points and CSV metadata. Photos without GPS remain in the photo and CSV output but are excluded from the point layer, with no placeholder coordinates generated.
- Original photos remain unchanged; photos and GPS are processed locally. HEIC preview availability may depend on Windows image codec support without preventing supported metadata reads.
- Tool entry: Design Tools → Field Research → Survey Photo Organizer.

**发布范围 / リリース範囲 / Release scope**

GitHub 正式发布，Microsoft Store 不在 v1.4.2 发布范围内；下一 Store 里程碑为 v1.5.0。
Microsoft Store には公開せず、GitHub のみで正式リリースします。次の Store マイルストーンは v1.5.0 です。
Officially released on GitHub; Microsoft Store is not included in v1.4.2. The next Store milestone is v1.5.0.
