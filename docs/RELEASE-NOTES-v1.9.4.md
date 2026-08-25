简体中文 | [日本語](RELEASE-NOTES-v1.9.4.ja.md) | [English](RELEASE-NOTES-v1.9.4.en.md)

# UrbanPlanToolbox v1.9.4 主题显示一致性修复

- 激活窗口恢复并固定浅色 `#E5F9F9`、深色 `#1A2323` 的导航表面，避免不同 Windows / Windows App SDK 环境下默认控件资源改变既有深青、浅青主题。
- 窗口失焦时，导航区域与系统标题栏同步进入非激活表面：浅色 `#F3F3F3`、深色 `#202020`；重新激活窗口后恢复青色。
- 明确区分 Active / Inactive × Light / Dark 四种状态，并继续让 High Contrast 使用系统颜色资源。
- 增加窗口激活状态的主题回归契约，覆盖事件订阅与释放、主题切换和非激活资源选择，降低不同电脑上出现 UI 漂移的概率。
- 项目 Schema、备份格式以及 GitHub / Microsoft Store 更新机制均未变化。
