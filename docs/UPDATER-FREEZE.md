简体中文 | [日本語](UPDATER-FREEZE.ja.md) | [English](UPDATER-FREEZE.en.md)

# 更新模块冻结合同

GitHub 与 Microsoft Store 两条运行时更新路径现均已完成真实环境验证并冻结。`UpdateViewModel` 继续作为应用级更新会话；`AboutPage` 在 `Loaded` 时连接、在 `Unloaded` 时解除连接，页面本身不拥有更新取消令牌。

GitHub 保留已经验证的版本发现、SHA-256、签名校验、部署与重启路径。Microsoft Store 保留一次用户操作触发 `RequestDownloadAndInstallStorePackageUpdatesAsync` 的 Windows 原生下载与安装流程，并在调用前注册 Windows 重启恢复。单个包的 `Completed` 不是整次更新事务的终态。页面导航必须保持检查状态、下载进度、本地化更新说明、目标版本、更新来源与可重试失败状态，且不得启动第二个 Store 更新操作。

2026-08-14 已完成真实 Microsoft Store **1.7.4 → 1.7.5** 端到端验收。至此 GitHub updater 与 Store updater 均为 **validated / fully frozen**，此前的 `final-e2e-pending` 状态正式结束。

冻结后不再以功能优化、交互微调或重构为理由修改 updater。只有确认存在 updater 缺陷、安全问题，或 Windows / Microsoft Store 平台与 API 兼容性要求时才允许重新打开该模块；任何此类修改都必须重新提供受影响渠道的完整端到端回归证据后才能再次冻结。
