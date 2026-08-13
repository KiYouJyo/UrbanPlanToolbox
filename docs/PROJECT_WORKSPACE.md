# 当前项目工作台合同

项目工作台以项目、搜索、设计工具、科研工具和项目归档为主要入口；关于与设置固定在底部。搜索继续使用既有稳定 ID 和独立一级入口；工具收藏保留，但不增加“常用功能”或 Favorites 一级入口。

## 数据模型与目录

每个项目使用不可编辑的 GUID。名称可以重复或重命名，但不会改变 ID 与目录：

```text
data/projects/index.json
data/projects/<project-guid>/project.json
attachments/projects/<project-guid>/
backups/projects/<project-guid>/project.json.last-valid.bak
```

`ProjectKind` 是顶层业务类别，稳定值只有 `design` 与 `research`；显示译文不参与逻辑。类别创建后不可修改，名称修改、语言切换均不改变类别、ID 或目录。类别内部另有独立 `ProjectType`：设计项目沿用 `coursework`、`competition`、`research`、`professional`、`personal`、`other`；研究项目使用 `coursework`、`thesis`、`paper`、`research-project`、`other`。

共同字段包含 ID、类别、名称、类型、自定义类型、重要时间节点、工作文件夹引用、归档状态和 UTC 时间。设计专属 `DesignDetails` 保存行政区、WGS 84 坐标、说明和规划要求；研究专属 `ResearchDetails` 保存研究领域、研究对象和研究方法。同一项目只能有一套有效专属数据，JSON 判别仅依赖稳定 `ProjectKind`，不使用运行时类型名或显示文字。当前 `ProjectSchemaVersion = 3`。

## 页面与状态

项目主页顶部使用与工具二级分类一致的横向选择交互，稳定分类 ID 为 `design-projects` 与 `research-projects`，默认设计项目并在当前会话记住选择。各分类只显示对应的未归档项目并按最近更新时间倒序。设计卡片显示类型、行政区和时间节点；研究卡片显示研究类型、领域、对象摘要和时间节点。归档页混合显示两类项目并明确标注类别。

两类工作台复用同一生命周期逻辑、重要时间节点模型和工作文件夹服务。设计模板保持行政区、坐标、说明和规划要求；研究模板提供名称/类型、研究领域、对象和方法的显式保存。离开未保存内容时提示，归档项目只读，恢复后可编辑。旧待办和规划指标快照继续留在 schema 兼容数据中，但当前工作台不展示。

## 工作文件夹授权

文件夹通过 Windows `FolderPicker` 与 `StorageApplicationPermissions.FutureAccessList` 授权。项目保存本机 token、显示名称和参考路径；应用不复制、扫描、监视或修改文件夹内容。token 不导出，导入后仅保留显示信息并标记需要重新选择。

## 扩展边界

当前不实现项目类别转换、“全部项目”分类、项目搜索/收藏/标签/模板、研究文献管理、在线学术服务、AI、云同步、协作、数据库或外部工作文件夹内容备份。

## Project Context 未来方向

未来可让项目与调研照片、GIS、坐标工具、图纸差异和输出结果建立关联。这是 planned / future direction，不代表这些关联已经实现。
