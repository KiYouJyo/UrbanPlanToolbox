# 规划工具箱（UrbanPlan Toolbox）

> 当前版本：`v0.4.0`（MSIX `0.4.0.0`）。面向 Windows 的城市规划辅助工具箱，项目、设置与规划输入均在本地处理。

> 已知限制：在部分冷启动环境中，原生 SplashScreen 结束后仍可能短暂显示带标题栏的纯黑窗口；该呈现问题不影响已验收功能，将在后续版本单独处理。

## v0.4.0 新增功能

- **色卡方案记录器**：在“设计工具 → 详细设计”中离线记录和管理配色方案。
- **多张项目原图**：支持上传并保存多张项目原图，图片作为应用内附件管理，不修改源文件。
- **色系分类**：方案支持按色系分类，并可增减具体颜色。
- **颜色类型、HEX 和 RGB**：每种颜色支持颜色类型标注，并记录 HEX 与 RGB 值。
- **数据与附件导出导入**：支持方案图片与数据的导出导入，延续既有带清单与 SHA-256 校验的安全备份流程。
- **工具开发模板**：固化新增工具开发模板和工具接入契约，见 [docs/TOOL_DEVELOPMENT_TEMPLATE.md](docs/TOOL_DEVELOPMENT_TEMPLATE.md)。
- 兼容现有项目与用户数据；`ProjectSchemaVersion`、`BackupFormatVersion` 保持仓库当前值不变。

## 已有工具

- **规划指标快速计算器**：容积率、建筑密度、绿地率、人口、户均、停车位和公共服务设施指标。
- **单位与比例尺换算器**：面向城乡规划、建筑设计、土地利用、场地设计与工程量估算。
  - 长度：公制、英美建筑单位与日本传统建筑单位。
  - 面积：公制、中国亩、日本坪/反/町及英美土地建筑单位。
  - 几何体积：公制与英美工程体积。
  - 比例尺：图上长度到实际长度、实际长度到图上长度的双向换算。
- **色卡方案记录器**（v0.4.0 新增）：离线记录项目原图、色系分类和可复用配色方案。

单位换算不包含液体容量单位，也不包含质量、温度、压力、能量、速度、货币或其他非规划建筑相关类别。亩、尺、坪、反和町属于地区性或传统单位；日本“畳/帖”面积标准不统一，未提供固定换算。英美几何单位采用国际英尺定义，不包含 U.S. survey foot。

## 应用功能概览

- 简体中文、日语和英语三语界面；可在设置页选择语言，重启后生效。
- 一级功能导航：项目、搜索、设计工具、科研工具和项目归档；关于与设置位于导航底部。
- 项目主页按稳定 `ProjectKind` 分为设计项目与研究项目；两类项目共用时间节点、归档/恢复/永久删除和受 FutureAccessList 授权保护的工作文件夹。
- 未来的重要时间节点可逐项启用 Windows 本地提醒；提醒随编辑、归档、删除、导入、语言切换和启动自动对账，详见 [项目时间节点提醒](docs/MILESTONE_REMINDERS.md)。
- 设置页可将项目、设置、收藏和应用内附件导出为带清单与 SHA-256 校验的 `.uptbackup`，也可在自动安全备份后执行完整替换导入。
- 设计工具和科研工具按稳定的二级分类浏览；已上线工具通过统一卡片展示并从工具注册表打开。
- 工具搜索页从 `ToolRegistry` 显示全部已上线工具，支持中文、说明、稳定 ID、完整拼音、拼音首字母和关键词的本地实时匹配；收藏工具会置顶并按拼音首字母分组。
- 示例数据、结果复制、浅色/深色/跟随系统主题和显示精度设置。

## 安装

正式发布为 x64 framework-dependent 自签名测试安装包（MSIX），通过 GitHub Releases 提供。安装步骤：

1. 从 [Releases](https://github.com/KiYouJyo/UrbanPlanToolbox/releases) 下载最新 `UrbanPlanToolbox-vX.Y.Z-x64-framework-dependent-self-signed.zip` 与 `SHA256SUMS.txt`。
2. 核对 SHA-256 后，将整个 ZIP 完整解压到同一文件夹（不要在压缩包预览中直接运行）。
3. 双击“① 安装规划工具箱.cmd”，接受 Windows UAC 提示，按脚本提示完成安装。
4. 安装完成后可通过开始菜单或 `shell:AppsFolder` 中的包身份入口启动应用。

卸载时双击“② 卸载规划工具箱.cmd”；脚本只卸载 UrbanPlanToolbox 并清理对应测试证书，不会卸载共享 Windows App Runtime，也不会删除 `%LocalAppData%\UrbanPlanToolbox` 中的用户数据。

该自签名包用于测试预览，不是正式受信任代码签名；完整构建、测试与打包说明见 [docs/RELEASE.md](docs/RELEASE.md) 与 [packaging/README.md](packaging/README.md)。

## v0.4.1 流程审核清单

- 新增 `workflow-review-checklist`，同一个 Stable ID 同时出现在设计工具→前期分析与科研工具→前期工具。
- 两个入口共享同一离线页面和 `data/tools/workflow-review-checklist/checklists.json` 数据；搜索与收藏只产生一个工具记录。
- 支持清单、流程阶段、审核项、四种审核状态、重点项、备注、筛选、统计、复制、排序和导入导出兼容。
- 使用独立 `WorkflowReviewChecklistSchemaVersion = 1`，不改变 `ProjectSchemaVersion` 或 `BackupFormatVersion = 1`。
- 当前仍为预览版本；Splash 后短暂黑色窗口属于既有已知限制。

## 技术与系统要求

C#、.NET 10、WinUI 3、Windows App SDK 和单项目 MSIX。支持 Windows 10 17763+，优先 Windows 11 x64。开发需要 Visual Studio 2026 的 WinUI 工作负载、Windows SDK 10.0.26100.0 和 .NET SDK 10。

## 本地隐私

规划输入仅在本机内存中计算；用户主动保存的项目快照才会写入本地项目文件。项目数据、偏好设置、附件、备份、缓存和日志分域管理。工作文件夹内容不会被复制、扫描、上传或导出；跨设备导入后必须重新选择文件夹。应用不提供帐户、云同步、遥测、广告或 AI 接口。“检查更新”仅在用户手动触发时匿名请求 GitHub Releases API。详见 [本地数据存储](docs/DATA_STORAGE.md)、[项目工作台](docs/PROJECT_WORKSPACE.md) 和 [数据备份与恢复](docs/DATA_BACKUP.md)。

## 构建、测试与发布

完整步骤见 [docs/RELEASE.md](docs/RELEASE.md)。面向 `main` 的 PR 和对 `main` 的推送都会自动运行 CI；CI 检查单元测试、Debug/Release x64 构建及 `packaging` 脚本，不生成正式签名安装包。

## 后续计划

版本路线图与版本/发布政策见 [docs/ROADMAP.md](docs/ROADMAP.md)。
## v0.4.3 设计理念词典

v0.4.3 adds the offline `design-concept-dictionary` tool under Design → Design Development. Concepts are stored at `%LocalAppData%\\UrbanPlanToolbox\\data\\tools\\design-concept-dictionary\\concepts.json` with independent schema version 1, atomic writes, last-valid recovery, future-schema protection, search/filter/sort, editable project-type and tag lists, and validated backup/import support. No attachments or external concept sources are bundled.

## Regulations index data

Version 0.4.2 packages a deterministic JSON snapshot at `Assets/Data/RegulationsIndex/regulations-index.v1.json`. It is an offline research index and official-portal directory, not legal advice, a complete legal corpus, a standards/PDF repository, or a compliance decision engine. Currentness, local adoption, paid standards, and applicable project requirements must be checked at the official source.
