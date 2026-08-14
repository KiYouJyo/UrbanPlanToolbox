简体中文 | [日本語](DATA_STORAGE.ja.md) | [English](DATA_STORAGE.en.md)

# 当前数据存储与迁移合同

## 数据分类与边界

UrbanPlanToolbox 将本地数据分为四类：

- **应用偏好设置**：主题、显示精度、自动计算、语言和收藏工具 ID。它们继续保存在既有 `%LocalAppData%\UrbanPlanToolbox\settings.json`，保持 v0.3.7 的键名与 JSON 格式，不强制迁移。
- **用户业务数据**：未来由色卡、调研、法规、灵感或项目归档等工具产生的结构化 JSON。它们必须通过统一路径与 `JsonDataStorage` 读写。
- **附件**：图片和文档等二进制文件，与结构化 JSON 分开存放。v0.3.8 尚未实现附件导入或管理。
- **缓存与诊断**：可再生成缓存和不含 payload 的诊断信息。清理它们不得删除设置、收藏、业务数据或附件。

## 本地目录结构

为兼容现有设置，v0.3.8 继续以 `%LocalAppData%\UrbanPlanToolbox` 作为应用数据根目录，而不改变 Package Identity、Publisher 或安装结构：

```text
UrbanPlanToolbox/
  settings.json
  data/
    projects/
      index.json
      <project-guid>/
        project.json
    tools/
      <stable-tool-id>/
        <tool-data>.json
  attachments/
    projects/<project-guid>/
  backups/
    projects/<project-guid>/project.json.last-valid.bak
    pre-import/<timestamp>/
    <stable-tool-id>/
      <tool-data>.json.last-valid.bak
  cache/
  logs/
```

`AppDataPathProvider` 集中生成这些路径。启动只建立基础目录；工具数据目录在首次请求时按需建立，不创建空业务文件。工具 ID 必须已存在于 `ToolRegistry`，且必须是单一安全路径段；本地化工具名称永远不参与路径生成。绝对路径、`..`、目录分隔符、非法文件名和非 JSON 数据文件名会被拒绝。

## JSON 数据信封

每个业务数据文件自带信封，因此当前不建立重复的全局 `manifest.json`：

```json
{
  "schemaVersion": 3,
  "savedAtUtc": "2026-08-01T00:00:00+00:00",
  "payload": {}
}
```

`schemaVersion` 是从 1 开始的整数，仅表示该业务文件的数据格式；它与应用版本 0.4.3、MSIX 版本 0.4.3.0 无关。当前项目格式为 `ProjectSchemaVersion = 3`，备份容器另用 `BackupFormatVersion = 1`。`savedAtUtc` 必须是 UTC。字段命名和 UTF-8 JSON 选项由 `DataStorageJson` 集中管理。

## 写入、备份与并发

`JsonDataStorage` 对同一规范化文件路径使用最小粒度的进程内互斥。保存流程为：

1. 将完整新信封写入正式文件同目录的唯一临时文件；
2. 刷新并关闭文件流；
3. 重新读取临时文件，确认信封可解析且版本准确；
4. 若正式文件存在且有效，将它复制到唯一备份临时文件，再替换该工具的一个 `last-valid.bak`；
5. 使用同卷替换操作将已验证临时文件替换为正式文件；
6. 尽力清理临时文件。清理失败不会把临时文件当作正式数据读取。

序列化、验证、备份或替换失败时返回 `IoFailure`，不会更新 `schemaVersion`，也不会主动删除原正式文件。备份固定为每个数据文件一个“最后有效版本”，不会无限累积。

## 读取、损坏恢复与未来版本保护

读取返回结构化状态：`Success`、`NotFound`、`RecoveredFromBackup`、`Corrupt`、`UnsupportedFutureVersion`、`MigrationFailed` 或 `IoFailure`。

- 正式文件不存在时返回 `NotFound`，不生成虚假默认业务数据。
- 正式文件或 payload 无法解析时，才检查最后有效备份。
- 备份有效时，先把损坏正式文件复制为带 UTC 时间和随机标识的诊断副本，再安全恢复；诊断副本保存在该工具的备份目录。
- 正式文件与备份都损坏时返回 `Corrupt`，不自动删除或重置。
- 文件版本高于当前服务支持版本时返回 `UnsupportedFutureVersion`；读写都不会覆盖、降级或把它误判为普通损坏。

## 迁移注册与失败处理

迁移步骤实现 `IDataMigration`，声明稳定名称、`FromVersion`、`ToVersion` 和对 JSON payload 的转换。`DataMigrationRunner` 只接受 `N -> N+1` 步骤，拒绝重复 `FromVersion`；执行时必须从文件版本逐步走到目标版本，缺少任何中间步骤都会失败。

迁移在内存中的 payload 副本上执行。所有步骤成功后，`JsonDataStorage` 才通过正常原子写入流程保存新信封并更新版本；中途失败返回 `MigrationFailed`，原文件、原版本和原数据保持可恢复。已经达到当前版本的数据不会重复迁移。当前连续步骤为 1→2（规划要求与重要时间节点）和 2→3（项目类别及专属 Details）。2→3 不猜测类别：所有既有项目都设为 `design`，旧设计字段迁入 `DesignDetails`，索引条目增加相同类别；原文件在替换前成为最后有效备份。单个项目失败只形成该项目 issue，不阻止其他项目读取。

## 日志与隐私

存储服务只向可注入的 `IStorageDiagnostics` 报告时间、操作类别、稳定工具 ID、schema 版本、迁移名称、结果和异常类型。安全默认实现不落盘。诊断事件不得包含完整 JSON payload、用户文本、项目内容、附件内容、密钥、证书或与排错无关的绝对路径。若未来增加文件日志，必须限定大小或滚动保留，不能无限增长。

## 新工具接入

未来工具接入时应：

1. 在 `ToolRegistry` 使用稳定、非本地化的工具 ID；
2. 通过 `IAppDataPathProvider` 获取自己的工具目录，不自行拼接应用根路径；
3. 定义纯业务数据模型和当前整数 schema 版本；
4. 如有旧版本，注册连续、可测试、尽量幂等的 `IDataMigration`；
5. 通过 `JsonDataStorage` 读取与保存，并明确处理所有结构化状态；
6. 将二进制附件放入附件区域，只在 JSON 中保存稳定的相对引用，不保存本机绝对路径。

## 项目域

项目目录使用不可变 GUID，不使用项目名或本地化文字。`index.json` 只保存 ID、`ProjectKind`、名称、稳定类型、归档状态和更新时间；完整设计/研究正文、兼容字段和文件夹引用保存在各自 `project.json`。正文与索引类别在成功保存后同步，读取列表时逐个加载正文，单个项目损坏不会阻止其他项目。归档仅修改状态与时间，不移动目录；永久删除使用暂存墓碑与索引回滚，不触碰外部工作文件夹。

项目正文继续通过 `JsonDataStorage` 使用 UTF-8 信封、原子写入、最后有效备份、损坏诊断与未来版本拒绝。`ProjectKind` 只能是 `design` 或 `research`，创建后保存层拒绝更改；两类专属 Details 互斥。备份包格式与导入替换流程见 [DATA_BACKUP.md](DATA_BACKUP.md)。

## 明确未实现

本版本不实现数据库、云同步、账户、多设备授权迁移、项目类别转换、外部工作文件夹内容备份、备份加密或合并导入。设置页“重置偏好”仍不会删除项目数据。

## Schema 变更要求

产品版本、`ProjectSchemaVersion`、`ToolSchemaVersion` 与 `BackupFormatVersion` 是不同的契约。未来任何 schema 变化必须定义版本号和迁移函数，并具备升级、未来版本拒绝和备份兼容性测试。
