简体中文 | [日本語](RELEASE-NOTES-v1.8.2.ja.md) | [English](RELEASE-NOTES-v1.8.2.en.md)

# UrbanPlanToolbox v1.8.2 WebDAV 云存档

- 扩展“设置 > 数据管理”，加入 WebDAV 云存档，同时保持本地数据为主数据源，网络不可用不会影响本地项目和工具。
- 复用现有 `.uptbackup` 备份格式创建云存档；上传前先在本机生成并验证备份，远端采用临时文件上传后 `MOVE` 为正式文件，避免中断上传留下伪完整存档。
- 支持配置与测试 WebDAV 连接、手动创建云存档、列出历史存档、从云端安全恢复以及删除指定远端存档。
- 云端恢复仍经过现有备份 manifest、SHA-256、格式版本与导入前安全备份/失败回滚流程。
- WebDAV 密码使用 Windows Credential Locker 保存，不写入 `settings.json`、`.uptbackup` 或日志；HTTP 连接会提示优先使用 HTTPS。
- “清空所有本地数据”会清除本机 WebDAV 配置和凭据，但不会自动删除远端存档。
- 将原有“导出备份 / 导入备份 / 清空所有本地数据”三个按钮改为水平排列，减少数据管理卡片的纵向占用。
