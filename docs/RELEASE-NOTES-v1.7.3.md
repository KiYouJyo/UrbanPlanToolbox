简体中文 | [日本語](RELEASE-NOTES-v1.7.3.ja.md) | [English](RELEASE-NOTES-v1.7.3.en.md)

# UrbanPlanToolbox v1.7.3 Microsoft Store 更新后重新启动修复

- 修复 Microsoft Store 更新安装后应用关闭但不会自动重新启动的问题。
- 在 Store 包部署开始前注册 Windows 应用重启恢复；若安装返回且旧进程仍存活，则由应用级重启完成版本切换。
- 完善取消、安装失败和重启失败时的恢复与重试逻辑。
- 保持“先下载、后安装”的 Store 两阶段更新、GitHub 更新和单实例行为不变。
