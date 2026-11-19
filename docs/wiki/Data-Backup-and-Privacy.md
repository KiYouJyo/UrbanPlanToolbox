# 数据、备份与隐私

UrbanPlanToolbox 采用**本机数据权威、离线优先**的设计。项目、灵感、工具数据与设置默认保存在用户设备上；WebDAV 功能提供的是可选“云归档”，而不是账户式实时同步。

## 数据放在哪里

应用继续使用：

```text
%LocalAppData%\UrbanPlanToolbox
```

作为主要本地数据根目录。典型结构包括：

```text
UrbanPlanToolbox/
  settings.json
  data/
    projects/
    tools/
  attachments/
  backups/
  cache/
  logs/
```

设置、业务数据、附件、缓存和诊断信息有明确边界。清理缓存不应删除项目、收藏、设置或附件。

## 业务数据保护

结构化业务数据通过统一 JSON 存储层写入，并使用版本化信封：

```json
{
  "schemaVersion": 3,
  "savedAtUtc": "2026-08-01T00:00:00+00:00",
  "payload": {}
}
```

实际 schema 版本由各业务合同决定，不能和应用版本号混为一谈。

保存流程包含：

- 临时文件完整写入
- 重新解析验证
- 保留最后有效备份
- 同卷原子替换
- 损坏恢复
- 未来版本拒绝
- 连续迁移步骤

如果文件版本高于当前应用支持范围，应用应拒绝覆盖或降级，而不是把它误判为普通损坏。

## `.uptbackup` 完整备份

当前备份容器格式为 **BackupFormatVersion 2**，扩展名为 `.uptbackup`，底层是带清单的 ZIP 容器。

备份可以包含：

- 设置
- 项目索引
- 项目正文
- 应用内项目附件
- 项目类别与专属资料
- 归档状态

清单记录文件相对路径、大小和 SHA-256，并记录备份创建时间及导出版本等信息。

### 不包含什么

完整备份不会包含：

- cache / logs
- 临时文件
- last-valid 内部备份
- 损坏诊断副本
- 证书 / 签名材料
- Windows 工作文件夹授权 token
- 外部工作文件夹的实际内容
- 应用运行时或安装包

## 导入为什么是“完整替换”

当前 `.uptbackup` 不做数据集合并。导入前应用会验证格式版本、manifest、SHA-256、文件数量、大小、schema、路径安全与未声明文件。

用户确认后，应用先在本机建立**预导入安全备份**；如果这一步失败，则停止导入。正式替换失败时，会尝试从该安全备份恢复。

导入到另一台电脑后，原工作文件夹授权 token 不会迁移，需要重新选择对应文件夹。

## WebDAV 云归档

v1.8.2 在 **设置 → 数据管理** 中加入 WebDAV 云归档。

它复用 `.uptbackup` 机制：

1. 先在本机创建并验证归档；
2. 上传为远端临时名称；
3. 上传完成后使用 WebDAV `MOVE` 提升为正式归档，避免半上传文件被误认为完整备份；
4. 恢复时仍走 manifest、SHA-256、格式版本、预导入安全备份和回滚验证。

支持的管理操作包括连接测试、手动创建归档、查看历史、恢复以及删除远端归档。

### WebDAV 不是实时同步

WebDAV 不会把本机变成“云数据库”，也不会实时合并多台电脑上的项目变更。本机数据仍是权威源。

因此旧的数据存储合同中“无云同步”的原则与 WebDAV 云归档并不矛盾：**有远端归档，不等于有多设备实时同步。**

## WebDAV 凭据

WebDAV 密码保存在 **Windows Credential Locker**，不会写入：

- `settings.json`
- `.uptbackup`
- 应用日志

如果使用纯 HTTP WebDAV 端点，应用会提示其安全风险。优先使用 HTTPS。

清除本地数据会移除本机 WebDAV 配置和凭据，但不会自动删除远端已经存在的归档。

## 隐私与联网边界

核心功能不要求账户或持续联网。照片、GPS、坐标和项目数据主要在本机处理。

联网行为主要发生在用户主动使用以下功能时：

- 检查 / 下载应用更新
- 检查 / 下载 Data Pack
- 访问 WebDAV 云归档

诊断设计要求不得记录完整业务 payload、用户文本、项目正文、附件内容、密钥、证书或与排错无关的绝对路径。

## 备份建议

- 在批量导入、重装系统或迁移电脑前先导出 `.uptbackup`。
- 重要项目可额外保留一份离线备份。
- 使用 WebDAV 时仍建议保留至少一份独立本地副本。
- 不要把外部工作文件夹已经被备份进 `.uptbackup` 当成理所当然——它们明确不在备份范围内。

## 相关正式文档

- [`docs/DATA_STORAGE.md`](https://github.com/KiYouJyo/UrbanPlanToolbox/blob/main/docs/DATA_STORAGE.md)
- [`docs/DATA_BACKUP.md`](https://github.com/KiYouJyo/UrbanPlanToolbox/blob/main/docs/DATA_BACKUP.md)
- [`PRIVACY.md`](https://github.com/KiYouJyo/UrbanPlanToolbox/blob/main/PRIVACY.md)