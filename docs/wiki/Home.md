# UrbanPlanToolbox Wiki

欢迎来到 **UrbanPlanToolbox** Wiki。

UrbanPlanToolbox 是一款面向城乡规划、建筑设计与空间研究的 **离线优先 Windows 工具箱**。本 Wiki 面向实际使用者与贡献者，重点回答“怎么安装、怎么用、数据在哪里、各功能之间是什么关系”；版本与发布状态则继续以仓库中的单一事实源为准。

> 本 Wiki 基线按 UrbanPlanToolbox **v1.9.3** 整理。当前状态以 [`docs/project-status.json`](https://github.com/KiYouJyo/UrbanPlanToolbox/blob/main/docs/project-status.json) 为准，历史变化以 [`CHANGELOG.md`](https://github.com/KiYouJyo/UrbanPlanToolbox/blob/main/CHANGELOG.md) 与 [GitHub Releases](https://github.com/KiYouJyo/UrbanPlanToolbox/releases) 为准。

## 从这里开始

- **第一次使用** → [快速开始](Getting-Started.md)
- **想了解有哪些功能** → [功能总览](Feature-Guide.md)
- **管理设计 / 研究项目** → [项目与工作台](Projects-and-Workspace.md)
- **查询法规、术语和设计理念** → [专业知识库与 Data Pack](Professional-Libraries.md)
- **随手记录灵感、使用后台驻留** → [灵感记录器与后台驻留](Inspiration-and-Background.md)
- **导出、恢复或云端归档** → [数据、备份与隐私](Data-Backup-and-Privacy.md)
- **切换语言或参与翻译** → [本地化与三语支持](Localization.md)
- **遇到安装、更新或数据问题** → [故障排除](Troubleshooting.md)
- **想参与开发或维护数据** → [贡献指南](Contributing.md)
- **查看版本政策与后续方向** → [版本与路线图](Releases-and-Roadmap.md)

## 当前产品边界

当前稳定线为 Windows x64，核心设计原则是：

1. **离线优先**：项目、工具数据和专业库安装后均可在本机使用；核心功能不要求账户。
2. **工具彼此独立**：项目工作台负责组织项目，但不会强迫所有计算器、GIS 或资料工具进入单一流程。
3. **数据可迁移**：项目数据使用明确的 schema 版本；完整备份使用 `.uptbackup` 容器并进行清单与 SHA-256 校验。
4. **专业数据独立版本化**：法规、术语和设计理念由独立 Data Pack 提供，可检查版本、导入本地包并回滚。
5. **发行渠道独立**：GitHub 旁加载版与 Microsoft Store / WinGet `msstore` 版拥有不同包身份和更新链，不能互相覆盖升级。

## 文档边界

Wiki 是“使用与理解”的入口，不替代以下正式文档：

- 当前版本与发行状态：[`docs/project-status.json`](https://github.com/KiYouJyo/UrbanPlanToolbox/blob/main/docs/project-status.json)
- 历史变更：[`CHANGELOG.md`](https://github.com/KiYouJyo/UrbanPlanToolbox/blob/main/CHANGELOG.md)
- 发布与签名流程：[`docs/RELEASE.md`](https://github.com/KiYouJyo/UrbanPlanToolbox/blob/main/docs/RELEASE.md)
- 数据存储合同：[`docs/DATA_STORAGE.md`](https://github.com/KiYouJyo/UrbanPlanToolbox/blob/main/docs/DATA_STORAGE.md)
- 备份格式合同：[`docs/DATA_BACKUP.md`](https://github.com/KiYouJyo/UrbanPlanToolbox/blob/main/docs/DATA_BACKUP.md)
- 本地化工程规范：[`docs/LOCALIZATION.md`](https://github.com/KiYouJyo/UrbanPlanToolbox/blob/main/docs/LOCALIZATION.md)
- 隐私政策：[`PRIVACY.md`](https://github.com/KiYouJyo/UrbanPlanToolbox/blob/main/PRIVACY.md)

如果 Wiki 与这些正式文档发生冲突，应以正式文档和当前代码为准，并同步修正 Wiki。