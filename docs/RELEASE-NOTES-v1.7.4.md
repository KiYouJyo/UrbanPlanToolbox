简体中文 | [日本語](RELEASE-NOTES-v1.7.4.ja.md) | [English](RELEASE-NOTES-v1.7.4.en.md)

# UrbanPlanToolbox v1.7.4 Microsoft Store 更新流程优化

- Microsoft Store 更新恢复为 Windows 官方的一体式下载与安装流程，避免两阶段更新导致系统授权窗口出现在语义错误的时间。
- 点击“下载并安装更新”后，由 Windows / Microsoft Store 依次处理下载和安装授权；在更新开始前注册 Windows 应用重启恢复，确保应用关闭后自动重新启动新版。
- 若 Store 更新完成后旧进程仍然存活，则继续使用应用级重启作为兜底；GitHub 更新、单实例机制以及其他功能保持不变。
