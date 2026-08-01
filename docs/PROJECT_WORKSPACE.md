# 项目工作台

v0.3.9 将原欢迎主页改造为项目主页。一级导航依次为项目、搜索、设计工具、科研工具和项目归档；关于与设置固定在底部。搜索继续使用既有稳定 ID 和独立一级入口；工具收藏保留，但不增加“常用功能”或 Favorites 一级入口。

## 数据模型与目录

每个项目使用不可编辑的 GUID。名称可以重复或重命名，但不会改变 ID 与目录：

```text
data/projects/index.json
data/projects/<project-guid>/project.json
attachments/projects/<project-guid>/
backups/projects/<project-guid>/project.json.last-valid.bak
```

项目保存名称、稳定类型值（`coursework`、`competition`、`research`、`professional`、`personal`、`other`）、自定义类型、行政区、可选 WGS 84 坐标、说明、待办、规划指标快照、工作文件夹引用、归档状态和 UTC 时间。显示译文与稳定类型值分离。`ProjectSchemaVersion = 1` 与应用版本无关。

## 页面与状态

项目主页仅显示未归档项目并按最近更新时间倒序排列。项目卡片显示类型、行政区、待办完成数、快照数和更新时间。工作台提供显式“保存修改”，离开未保存概览时提示；归档项目默认只读，恢复后可继续编辑。归档不是删除，只改变 `isArchived` 与 `archivedAtUtc`。

待办拥有稳定 GUID、标题、完成状态、创建/完成时间和显示顺序。规划指标快照复用现有 `PlanningInput`、`PlanningCalculationService` 与 `PlanningResult`，保存完整 decimal 值和 null，不受界面显示精度影响；更新指标时创建新快照，不覆盖历史记录。

## 工作文件夹授权

文件夹通过 Windows `FolderPicker` 与 `StorageApplicationPermissions.FutureAccessList` 授权。项目保存本机 token、显示名称和参考路径；应用不复制、扫描、监视或修改文件夹内容。token 不导出，导入后仅保留显示信息并标记需要重新选择。

## 扩展边界

v0.3.9 不实现项目全文搜索、项目与工具混合搜索、永久删除、标签、缩略图、模板、在线地图、地理编码、云同步、协作、文件版本控制或 Git 集成。
