# UrbanPlanToolbox v1.4.2

## 调研照片整理器

v1.4.2 新增面向规划、设计与 GIS 实地调研的本地照片整理工作流。

### 主要功能

- 读取照片 EXIF、GPS、拍摄时间、海拔和方向信息。
- 浏览照片缩略图，使用自由输入的 Tags/标签和 Note/备注整理调研记录。
- 导出统一命名的照片副本、WGS 84 / EPSG:4326 Shapefile 点位和 CSV 元数据。
- 无 GPS 照片仍保留在照片和 CSV 输出中，但不会进入 Shapefile 点图层，也不会生成假坐标。
- 原始照片保持不变；照片和 GPS 仅在本机处理，不上传照片或定位数据。

### 使用流程

导入照片 → 检查 EXIF/GPS 与缩略图 → 添加标签和备注 → 选择输出目录 → 导出 GIS 调研包。

支持批量选择和拖放导入 JPG、JPEG、HEIC、HEIF、PNG 照片。HEIC 预览可能依赖 Windows 图像编解码器，但不影响已支持的元数据读取。

### Microsoft Store

v1.4.2 通过 GitHub 发布。Microsoft Store 继续采用 `x.0.0` / `x.5.0` 里程碑策略，本版本跳过 Store；下一 Store milestone 为 v1.5.0。
