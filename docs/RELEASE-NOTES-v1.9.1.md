简体中文 | [日本語](RELEASE-NOTES-v1.9.1.ja.md) | [English](RELEASE-NOTES-v1.9.1.en.md)

# UrbanPlanToolbox v1.9.1 工程收口与版本治理

- 统一应用版本、程序集版本、GitHub 与 Store Manifest、候选发布元数据和项目状态 SSOT，消除 1.9.0 构建状态与旧 1.8.5 文档状态并存的问题。
- 加强 CI 文档一致性检查：项目状态中的产品版本和候选包版本必须与 `release/release.json`、项目文件及两个 Manifest 保持一致，今后的版本事实源漂移会直接阻断合并。
- 将写死 1.9.0 的签名验收流程改为读取当前发布元数据的版本无关流程，后续维护版本无需复制新的验收工作流。
- 补齐 1.8.1 至 1.9.0 的 CHANGELOG 历史，并为 1.9.1 建立三语与结构化运行时发布说明。
- 本轮不改变项目 Schema、备份格式、更新器机制或面向用户的功能；1.9.1 当前仅作为待验证候选，不自动创建 GitHub Release，也不提交 Microsoft Store。
