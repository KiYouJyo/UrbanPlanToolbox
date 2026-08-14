简体中文 | [日本語](StoreUpdateTesting.ja.md) | [English](StoreUpdateTesting.en.md)

# Microsoft Store 应用内更新 E2E 合同

任何 Store 更新行为的修改都必须提供真实端到端证据；单元测试、构建、打包、提交认证、公开发布或下载进度本身都不能替代真实设备上的应用内更新验收。

## 已完成的最终验收

- 来源：Microsoft Store 正式版 **1.7.4**
- 目标：Microsoft Store 正式版 **1.7.5**
- 验收日期：**2026-08-14**
- Store 发布状态：**PUBLISHED**
- Updater E2E 状态：**PASSED / FULLY FROZEN**

真实 Store 1.7.4 → 1.7.5 更新验收已经完成，因此此前的 `FINAL-E2E-PENDING / FREEZE-READY` 状态正式关闭。本次真实 E2E 是将 Microsoft Store updater 标记为 fully frozen 的最终依据，而不是仅以商店发布成功作为替代证据。

## 冻结后的固定路径

Microsoft Store 更新路径固定为：**现有 Store 安装 → 检查更新 → 显示可用版本与本地化更新说明 → 用户执行一次“下载并安装更新” → 在 Store 操作前注册 Windows restart recovery → Windows 原生下载与安装授权 → Store deployment → 自动启动新版本 → 保留用户数据**。

Store deployment 可能终止旧进程；若发生终止，由预先注册的 Windows restart recovery 负责重新启动。若 Store 操作返回时旧进程仍存活，则应用必须先注销 recovery registration，再仅执行一次 `AppInstance.Restart` 作为后备。包级 `Completed` 回调不是应用级终态，只有 await 的 Store operation `OverallState` 才是权威结果。取消或失败后必须恢复为可重试状态，并且页面离开后返回不得启动第二个 Store 操作。

## 重新打开条件

冻结后不再为功能扩展、交互微调或内部重构修改 Store updater。只有确认存在 updater 缺陷、安全问题，或 Windows / Microsoft Store 平台与 API 兼容性要求时才允许修改；一旦修改，必须重新执行受影响渠道的真实 E2E，并在证据通过后才能再次标记为 frozen。
