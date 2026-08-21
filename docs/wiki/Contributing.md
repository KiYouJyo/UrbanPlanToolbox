# 贡献指南

欢迎为 UrbanPlanToolbox 提交代码、文档、翻译、专业资料或问题报告。本页只提供协作入口；具体工程合同仍以仓库中的正式文档和测试为准。

## 开始之前

建议先阅读：

- [`README.md`](https://github.com/KiYouJyo/UrbanPlanToolbox/blob/main/README.md)
- [`docs/DOCUMENTATION.md`](https://github.com/KiYouJyo/UrbanPlanToolbox/blob/main/docs/DOCUMENTATION.md)
- [`docs/project-status.json`](https://github.com/KiYouJyo/UrbanPlanToolbox/blob/main/docs/project-status.json)
- [`docs/RELEASE.md`](https://github.com/KiYouJyo/UrbanPlanToolbox/blob/main/docs/RELEASE.md)
- [`docs/LOCALIZATION.md`](https://github.com/KiYouJyo/UrbanPlanToolbox/blob/main/docs/LOCALIZATION.md)
- [`docs/DATA_STORAGE.md`](https://github.com/KiYouJyo/UrbanPlanToolbox/blob/main/docs/DATA_STORAGE.md)
- [`docs/DATA_BACKUP.md`](https://github.com/KiYouJyo/UrbanPlanToolbox/blob/main/docs/DATA_BACKUP.md)
- [`CHANGELOG.md`](https://github.com/KiYouJyo/UrbanPlanToolbox/blob/main/CHANGELOG.md)

当前产品线是稳定的 1.x，贡献应优先保持兼容性、数据安全与既有更新链稳定。

## 获取代码与基础验证

仓库使用 WinUI 3 / C#，目标平台为 Windows x64。

基础恢复与测试命令：

```powershell
dotnet restore UrbanPlanToolbox.slnx -p:Configuration=Debug -p:Platform=x64
dotnet test UrbanPlanToolbox.slnx -c Debug -p:Platform=x64 --no-restore
```

正式打包、签名、Release 和 Microsoft Store 流程请严格遵循 [`docs/RELEASE.md`](https://github.com/KiYouJyo/UrbanPlanToolbox/blob/main/docs/RELEASE.md)，不要根据 Wiki 自行推断发布步骤。

## 修改功能时

### 保持工具独立

当前产品方向强调轻量项目组织，而不是把所有独立工具强行串成单一工作流。新增工具应有稳定、非本地化 ID，并通过现有注册与导航机制接入。

### 数据格式必须版本化

如果新增或修改持久化业务数据：

- 定义整数 schema 版本；
- 使用统一存储路径服务，不手工拼接本机路径；
- 通过统一 JSON 存储层读写；
- 对旧版本提供连续 `N -> N+1` 迁移；
- 明确处理损坏、未来版本和 I/O 失败；
- 不把本机绝对路径当作可移植业务数据；
- 二进制附件与 JSON 结构化数据分离。

产品版本、项目 schema、工具 schema 和备份格式版本是不同合同，不能互相代替。

### 不要随意修改已冻结更新器

当前 GitHub 与 Microsoft Store 更新机制都处于验证 / 冻结状态。除非存在已经证明的 bug、安全问题或 Windows 平台 / API 兼容性要求，并且能够提供端到端回归证据，否则不应为了“顺手重构”修改更新机制。

## 修改三语 UI 时

当前支持：

- `zh-CN`
- `ja-JP`
- `en-US`

新增或修改资源键时必须同时维护三套 `Resources.resw`，保持键集合和格式占位符一致。

静态 XAML 文本优先使用 `x:Uid`，动态文字使用 `ILocalizationService`。不要在页面内另建 `ResourceLoader`，也不要把在线机器翻译引入运行时 UI。

详见 [本地化与三语支持](Localization.md)。

## 修改专业知识库时

从 v1.9.2 起，法规、规划术语与设计理念的运行时数据已经从应用仓库解耦，正式资料应通过独立 **`KiYouJyo/UrbanPlanToolbox_Data`** Data Pack 体系维护。

贡献专业资料时应特别注意：

- 保留来源 / provider / provenance；
- 不把未经确认的推测写成事实；
- 三语字段遵循数据包现有 schema；
- 设计理念只收录有明确依据的概念，不根据图面擅自命名；
- 数据版本和应用版本独立管理；
- 修改后通过 Data Pack manifest、大小、SHA-256、路径与兼容性验证。

详见 [专业知识库与 Data Pack](Professional-Libraries.md)。

## 修改项目工作台时

项目工作台需要同时考虑：

- `design` / `research` 两类稳定项目类别；
- 归档项目只读；
- 12 列规范布局；
- 8 / 6 / 单列响应式呈现；
- 响应式显示不能覆盖用户保存的规范布局；
- 旧项目迁移与兼容；
- 用户数据在语言切换后仍保持原样。

## 文档怎么写

文档治理原则是**不要复制可变事实**。

- 当前产品 / 发布状态 → `docs/project-status.json`
- 历史变化 → `CHANGELOG.md` 与 Release Notes
- 发布流程 → `docs/RELEASE.md`
- 详细工程合同 → 对应 `docs/*.md`
- Wiki → 解释用户工作流、概念和常见问题

如果一个版本号、发布状态或 schema 已有单一事实源，Wiki 应链接它，而不是再维护一份容易过期的平行记录。

## 提交 PR 前建议检查

- 变更是否只覆盖预期范围；
- Debug x64 测试是否通过；
- 新增字符串是否三语齐全；
- 数据迁移是否有旧版 / 未来版本覆盖；
- 是否误改发布渠道、包身份或冻结更新机制；
- 用户数据是否可能被清空、覆盖或写入日志；
- CHANGELOG / 正式文档是否需要同步；
- 专业资料是否应该放在 Data Pack 仓库而不是应用仓库。

## 安全与隐私

Issue、PR、截图和日志中不要提交：

- 密码 / WebDAV 凭据
- 私钥或签名材料
- 用户项目正文
- 未脱敏的本机个人路径
- 不必要的照片 GPS / EXIF 个人信息

## 相关页面

- [故障排除与 FAQ](Troubleshooting.md)
- [本地化与三语支持](Localization.md)
- [专业知识库与 Data Pack](Professional-Libraries.md)
- [版本与路线图](Releases-and-Roadmap.md)