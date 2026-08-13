# 当前数据备份与恢复合同

## `.uptbackup` 格式

`.uptbackup` 使用 ZIP 容器，`BackupFormatVersion = 1`。包包含：

```text
backup-manifest.json
settings/settings.json
data/projects/index.json
data/projects/<project-guid>/project.json
attachments/projects/...
```

清单记录 UTC 创建时间、导出应用版本、项目/活跃/归档数量，以及每个内容文件的相对路径、大小和 SHA-256。导出先在隔离临时目录生成，完成后重新验证整个包，再移动到用户通过 `FileSavePicker` 选择的位置。

导出包括 `ProjectSchemaVersion = 3`、`ProjectKind`、互斥的 `DesignDetails`/`ResearchDetails`、重要时间节点、兼容保留字段、归档状态、设置、语言、主题、精度、自动计算、收藏工具 ID 和应用内项目附件。不包括 cache、logs、临时文件、last-valid 备份、损坏诊断副本、证书/签名材料、FutureAccessList token、外部工作文件夹内容、运行时或安装包。容器和 manifest 结构保持兼容，因此 `BackupFormatVersion` 继续为 1。

## 完整替换导入

当前不支持合并导入。`FileOpenPicker` 选中包后，应用先验证格式版本、清单、大小、SHA-256、文件数量、单文件与总大小、项目 SchemaVersion、相对路径、重复条目、未列出文件和危险扩展名。限制为最多 10,000 个文件、单文件 256 MiB、备份包 2 GiB。

用户确认完整替换后，应用在 `backups/pre-import/<timestamp>/` 创建最近一次内部安全备份。安全备份失败则停止，不修改正式数据。导入内容先进入暂存目录；替换失败时自动从预导入备份恢复并报告恢复结果。导入成功后建议重启，使语言与主题完全生效。

工作文件夹 token 不可跨电脑或安装迁移。导出仅保留显示名称与参考路径；导入后 token 为空且状态为“需要重新选择”，应用不会自动访问原路径。外部文件夹内容从不进入备份包。schema 2 项目包可通过检查并在首次读取时确定性迁移为设计项目；schema 3 的设计和研究项目按原 `ProjectKind` 恢复，未来 schema 继续拒绝。

未来修改 `BackupFormatVersion` 必须先定义 migration / compatibility contract，并验证旧备份升级、未来版本拒绝与完整恢复。
