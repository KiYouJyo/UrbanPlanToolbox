# 文档治理与 Single Source of Truth

UrbanPlanToolbox 的文档采用“当前事实单一来源 + 历史事实不可覆盖”的维护模型。目标是避免 README、ROADMAP、发布指南、网站和 Store 文档分别维护版本号与发布状态，从而产生互相矛盾的“当前版本”。

## 1. 当前事实的唯一来源

`docs/project-status.json` 是仓库内关于当前项目状态的 Single Source of Truth（SSOT）。凡涉及以下信息，其他文档不得自行维护另一套值：

- 当前产品版本；
- GitHub 当前正式 Release 与包版本；
- Microsoft Store 当前已提交版本和仓库最后确认的状态；
- 当前 GitHub / Store 更新路径；
- 双渠道发布政策；
- 当前产品化阶段和尚未绑定版本号的路线优先级。

如果某份文档需要说明这些事实，应引用 `project-status.json` 的语义并避免把同一字段复制成另一份“权威表”。

## 2. 构建版本与 SSOT

`UrbanPlanToolbox.csproj`、`Package.appxmanifest` 与 `Package.Store.appxmanifest` 仍是构建与打包链实际消费的版本输入，但它们不再被视为独立的文档状态源。正式发布准备时，它们必须与 `docs/project-status.json` 中的产品版本保持一致。

后续发布自动化应优先增加校验或生成步骤，使版本、Release Notes、网站版本历史和渠道发布元数据从同一发布元数据产生，而不是靠人工逐文件同步。

## 3. 历史事实的来源

- `CHANGELOG.md`：产品历史变化的主时间线。
- `docs/RELEASE-NOTES-vX.Y.Z.md` 与 `Assets/Data/ReleaseNotes/*.json`：对应版本的用户可见更新说明。
- GitHub Releases：GitHub 渠道是否真正发布的外部事实来源。
- Partner Center / Microsoft Store 客户端：Store 是否通过认证并真正公开的最终事实来源。

历史 Release Notes 原则上不因后续架构变化而重写；只有当发布边界本身记录错误时，才可做明确的事实纠正。

## 4. 各文档职责

### README

README 只说明产品定位、核心功能、获取方式、宏观安装更新方式、隐私、系统要求和文档入口。README 不承担逐版本更新日志，也不复制完整当前发布状态表。

### ROADMAP

ROADMAP 说明“当前处于什么产品化阶段、下一阶段解决什么问题、什么不应现在做”。不为尚未批准的工作擅自分配版本号或发布日期，也不维护一套独立的当前版本记录。

### RELEASE

RELEASE 是可复用的发布合同和 release gate。它说明一次 GitHub 或 Store 发布需要满足什么条件，不再堆叠已经结束的历史版本发布决策。

### STORE-PUBLISHING

STORE-PUBLISHING 只说明 Store 身份、工作流、认证边界、失败恢复与 Partner Center 事实来源。当前提交状态读取 SSOT，不把旧 Submission 当成“当前状态”。

### RELIABILITY

RELIABILITY 定义启动、异步操作、更新、日志、数据恢复和发布验证的可靠性边界。

### DATA / PROJECT / LOCALIZATION 等专题文档

专题文档描述稳定的数据格式、项目模型、本地化约定和工具开发约束。若某项规则已跨越多个版本持续有效，应使用“当前格式/当前合同”而不是把它写成某个早期版本的临时实现。

## 5. 运行时更新文件不是文档 SSOT

`docs/update-manifest.json` 和 `docs/UrbanPlanToolbox.appinstaller` 属于运行时或兼容性更新基础设施。它们可能为了客户端更新机制保留不同字段，不能被 README、ROADMAP 或发布文档当作当前发布状态的唯一依据。

## 6. 更新顺序

一次正式版本推进应遵循以下顺序：

1. 批准版本和发布范围；
2. 更新统一发布元数据 / `project-status.json` 候选状态；
3. 校验项目文件、两个 Manifest、三语 Release Notes 和渠道包版本；
4. 完成测试、构建、安装和更新 E2E；
5. 发布 GitHub 或提交 Store；
6. 依据外部真实结果把 SSOT 状态从 prepared/submitted 更新为 published/certification-submitted 等准确状态；
7. 网站和面向用户的当前状态展示只消费该统一元数据或由它生成。

认证中、上传完成、构建成功、下载 100% 都不得被写成“已发布”或“更新完成”。

## 7. 当前治理目标

当前阶段不推进新的产品版本。文档治理的目标是先消除陈旧版本状态、旧 updater 描述、过时 Store 里程碑规则和版本绑定式专题说明，并为后续自动生成 Release Notes、网站版本历史及发布元数据建立清晰边界。

## 8. Current documents

### Product

- [Roadmap](ROADMAP.md)
- [README](../README.md)

### Release

- [Release contract](RELEASE.md)
- [Microsoft Store publishing contract](STORE-PUBLISHING.md)
- [Store updater E2E contract](StoreUpdateTesting.md)

### Architecture

- [Data storage](DATA_STORAGE.md)
- [Data backup](DATA_BACKUP.md)
- [Project workspace](PROJECT_WORKSPACE.md)
- [Localization](LOCALIZATION.md)
- [Reliability](RELIABILITY.md)
- [Interaction components](INTERACTION_COMPONENTS.md)
- [Tool development template](TOOL_DEVELOPMENT_TEMPLATE.md)

### Historical

- [Changelog](../CHANGELOG.md)
- [Release notes](.)
- [Historical release decisions](history/release-decisions-1.4-1.5.md)
