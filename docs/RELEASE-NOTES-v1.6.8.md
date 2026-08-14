简体中文 | [日本語](RELEASE-NOTES-v1.6.8.ja.md) | [English](RELEASE-NOTES-v1.6.8.en.md)

# UrbanPlanToolbox v1.6.8 维护收口版

- GitHub updater 已真实验收并冻结；仅已证实 bug、安全问题或 Windows 平台/API 兼容性可改变它。
- v1.6.8 是 Store updater 的最终真实 E2E 目标，当前为 **PENDING**，不宣称已通过。
- 当前正式文档补齐 zh-CN、ja-JP、en-US sibling files，并由 `project-status.json` 统一当前状态。
- 首次启动向导背景改为浅色淡青色、深色深青色；保持布局、焦点和生命周期。
- 建立启动、包体和依赖基线，完成 Light/Dark/DPI 维护性回归。
- 修复卸载重装或 Windows 重置后保留业务数据会错误跳过首次使用向导的问题。
- 统一应用内弹窗、下拉选择器及其他浮层界面的主题 Surface，使其与应用 Light、Dark 和系统主题视觉保持一致。
