# 更改日志

## 1.6.6

### Changed

- Improved visual continuity between the startup overlay and the main-window Mica backdrop.
- Kept the startup logo visible for at least 500 ms on fast launches, while longer initialization naturally extends the presentation.
- Refined the startup fade-out transition.
- Unified Light and Dark startup-logo sizing, sharpness, and DPI behavior.
- Persist the last normal window size and maximized state, with safe restoration when the display work area changes.

### Release boundary

- Released through GitHub Releases only; Microsoft Store is not part of this release.

## 1.6.5

### Fixed

- Replaced incorrect certificate-file parsing for MSIXBundle verification with Windows Authenticode/MSIX validation.
- Verify the signed package with WinVerifyTrust, extract the signer from AppxSignature.p7x, and pin both Subject and Thumbprint.
- Preserve granular checksum and signature verification failures, with negative security coverage for invalid, unsigned, and mismatched signers.

### Release boundary

- GitHub Releases only; Microsoft Store is not included.
- v1.6.5 fixes bundle verification. The next real updater end-to-end acceptance target is v1.6.5 to v1.6.6 after manually installing the official v1.6.5 release.

## 1.6.4

### Changed

- Unified the visual background of NavigationView's expanded and small-window overlay panes for both light and dark themes.
- Preserved the existing responsive pane behavior, pane measurements, navigation items, main-window Mica backdrop, and content layout.

### Release boundary

- GitHub Releases only; Microsoft Store is not included.
- This release is the real-world target for validating the v1.6.3 GitHub restart-and-update flow after publication.

## 1.6.3

### Fixed

- Stabilized update-management card fields and removed empty dash placeholders.
- Replaced overlapping checking-state field indicators with one status progress indicator.
- Fixed packaged, locale-matched Release Notes loading for zh-CN, ja-JP, and en-US, including LocalVersionNewer results.
- Hardened the GitHub update state machine against late progress callbacks and added Downloading → Verifying → ReadyToInstall.

### Release boundary

- GitHub Releases only; Microsoft Store is not included.
- The real restart-and-update end-to-end acceptance remains pending for the v1.6.3 → v1.6.4 upgrade path.

## 1.6.2

### Fixed

- GitHub update-management cards now keep their fields and release metadata stable while checking, downloading, and waiting for installation.
- Inline progress indicators remain attached to their corresponding update fields.
- GitHub UpToDate results now retain the latest Release Notes for display.

### Release boundary

- GitHub-only release; Microsoft Store submission is not included.
- v1.6.1 to v1.6.2 real upgrade acceptance remains pending after publication.

## 1.6.1

### Fixed

- GitHub sideload updates now preserve release metadata while downloading and expose a distinct `ReadyToInstall` state.
- Added a user-triggered “Restart and update” action after the verified package is ready.
- GitHub installation continues through Windows package deployment; Microsoft Store was not part of this release.

### Release

- GitHub package version: `1.6.1.0`.
- GitHub Release tag: `v1.6.1`.

## 1.5.8

### Fixed

- Restored the real GitHub and Microsoft Store update download ProgressBar.
- Restart the application through the existing ApplicationRestartService after a completed update, with a localized manual-restart fallback when Windows returns a failure reason.
- Fixed the GitHub one-click uninstall contract to use `-RemoveCertificate` and remove only the GitHub package identity.

### Release

- GitHub package version: `1.5.8.0`.
- Microsoft Store package version: `1.5.8.0`.
- GitHub and Microsoft Store continue to use independent update pipelines.

## 1.5.7

### Changed

- Unified update-check UI across GitHub and Microsoft Store distributions.
- Added localized themed update confirmation dialogs.
- Removed user-visible update progress percentage and progress bar.
- Separated installed and available version display semantics.

### Fixed

- Fixed raw GitHub Release body appearing in the About page.
- Fixed incorrect current-version display in the update card.
- Fixed copyright-symbol encoding corruption.

## 1.5.6（GitHub 正式发布）

- GitHub 旁加载版支持用户主动触发的 App Installer 应用内更新。
- GitHub 与 Microsoft Store 更新渠道保持隔离。
- 首次安装一键包改为证书 bootstrap 加 Windows App Installer 安装链。
- 增加 Legacy GitHub 安装迁移支持。
- 本版本发布范围为 GitHub Release；不包含 Microsoft Store 操作。

## 1.5.4（GitHub 正式发布；Microsoft Store 正式提交）

### Fixed

- 修复更新确认弹窗标题可能显示本地化资源键的问题。
- 统一 Microsoft Store progress bridge，改用 `AsTask(cancellationToken, IProgress<StorePackageUpdateStatus>)`。
- 改进总体下载进度、package 进度和实际下载字节数的映射与回退处理。
- 修复无效 callback 清空已有下载进度的问题。
- 修复离开 About 页面可能取消 Store 更新操作的问题。

### Diagnostics and localization

- 增强 Store 更新状态、进度来源和版本诊断日志。
- 完善简体中文、日语和英语更新界面资源及 PRI 验证。

### Distribution

- GitHub 正式发布 v1.5.4。
- Microsoft Store 提交正式 v1.5.4.0；认证和公开状态以 Partner Center 及用户端可获取状态为准。

## 1.5.2（GitHub 正式发布；Microsoft Store 提交认证）

### Fixed

- 修复更新弹窗标题错误显示本地化资源键的问题。
- Microsoft Store 更新下载显示真实进度，并按 Store 状态区分下载、部署、完成、失败和取消。
- 改进进度边界保护、失败恢复和重复更新操作保护。

### Localization

- 保持简体中文、日语和英语更新界面资源一致。

### Distribution

- GitHub 正式发布 v1.5.2。
- Microsoft Store 通过现有流程提交认证；认证和公开状态以 Partner Center 及用户端可获取状态为准。

## 1.5.1（GitHub 正式发布；Microsoft Store 一次性例外提交）

### Added

- 新增“坐标点批量格式转换器”，支持 DD、DDM、DMS 坐标格式的自动识别、转换与合法性检查。
- 支持批量粘贴以及 CSV、TSV、TXT 导入，保留其他字段并导出标准化 CSV。
- 增加经纬度顺序检测、歧义提示和异常坐标检查。

### Integration

- 集成到工具注册表、搜索、收藏和三语导航入口。
- 输出格式与小数位数变更会即时刷新结果，并同步作用于复制和 CSV 导出。

### Privacy

- 坐标处理完全在本机完成，不上传用户坐标数据。

### Distribution

- GitHub 正式发布 v1.5.1。
- 本版本按一次性 Store 例外执行 Microsoft Store 提交；认证完成前不宣称已公开，后续仍恢复 `x.0.0` / `x.5.0` 里程碑节奏。

## 1.4.2（GitHub 正式发布）

### Added

- 新增“调研照片整理器”（设计工具 → 实地调研）。
- 读取照片 EXIF、GPS、拍摄时间、海拔和方向，并提供缩略图预览。
- 支持自由输入的 Tags/标签和 Note/备注。
- 导出统一命名的照片副本、WGS 84 / EPSG:4326 Shapefile 点位和 CSV 元数据。

### Improved

- 支持批量导入、拖放导入和单张失败隔离。
- 无 GPS 照片保留在照片和 CSV 输出中，不生成假坐标，也不进入 Shapefile 点图层。
- 原始照片只读；照片和 GPS 仅在本机处理，日志不记录 GPS 或完整照片路径。
- HEIC/HEIF 元数据读取支持大小写不敏感的扩展名筛选；HEIC 预览可能依赖 Windows 图像编解码器。

### Release policy

- v1.4.2 仅发布 GitHub；Microsoft Store 按里程碑政策跳过，下一 Store milestone 为 v1.5.0。

## 1.4.1（GitHub 正式发布）

- 新增中日英规划术语库：140 条核心术语，提供中文、日文、英文三语对照。
- 支持中文、日本、通用概念分类，多语言搜索、别名检索、术语关系、易混淆概念辨析和来源信息。
- 工具入口覆盖设计工具 → 前期分析、科研工具 → 前期工具。
- 完成响应式布局与相关 UI 修复；宽窗口双栏底部的小幅不齐列为 deferred。
- v1.4.1 仅发布 GitHub，Microsoft Store 按渠道政策不触碰。

## 1.4.0（GitHub 正式发布）

### Foundation 与架构

- 完成工具注册、设置、发行渠道、数据模式、导入导出、备份和启动管线的 Foundation 更新。
- 增加本地日志、诊断信息复制/日志文件夹入口、安全保留策略和启动耗时记录。
- 保持 GitHub 旁加载与 Microsoft Store 的 Identity、Publisher、更新路径和发布节奏独立。

### UI/UX 与隐私

- About 页面补齐统一卡片布局，并将诊断与日志操作收纳在独立卡片中。
- 首次启动向导提供四步引导；“查看完整隐私政策”打开在线隐私政策：<https://kiyoujyo.github.io/UrbanPlanToolbox/privacy/>。
- 保持离线优先设计；诊断日志仅保存在本机，不会自动上传。

### 发布说明

- 本版本作为 GitHub 最新正式版本发布。
- Microsoft Store 按里程碑节奏跳过 v1.4.0；下一 Store 里程碑为 v1.5.0。
- Splash Logo 的后续优化保留为已知延期事项，不影响本版本发布。

## 1.3.0（已正式发布）

### 新增

- 支持在简体中文、日语和英语之间即时切换界面语言，无需重新启动应用。
- 项目时间节点提醒统一移动到设置页，并新增“不重复”、6 小时、12 小时、24 小时和 3 天重复提醒间隔。
- 每个时间节点在首次提醒后最多重复提醒 3 次；未指定具体时间的节点继续在当天 09:00 首次提醒。

### 改进

- 集中化本地化、页面状态恢复和项目时间节点提醒调度架构。
- 重复提醒使用稳定通知系列 ID，支持去重、过期过滤、修改后替换和删除/归档取消。
- 保持现有项目、归档、设置以及导入导出数据兼容。

## 1.2.1（历史发布版本）

### 修复

- 修复 Store 包运行时渠道识别未读取正式 Package Identity、导致“关于”页面显示为 GitHub 的问题。
- 恢复 GitHub 旁加载版的 GitHub Releases 更新检查，并将 Store 更新 API 限定在正式 Store Identity。
- 增加手动触发的 Microsoft Store 草稿上传工作流；上传、认证和发布阶段保持独立。

## 1.2.0（历史发布版本）

### 新增

- Microsoft Store 安装版支持在应用内检查、下载和安装 Store 更新。
- 项目采用 MIT License 开源，并新增 GitHub 仓库、Releases、Issue 和许可证入口。

### 改进

- 重做“关于”页面，采用与设置页一致的左对齐卡片布局。
- 完善应用、隐私、开源与双发行渠道说明，以及中英日三语文本。

### 修复

- 修复 About 页面内容居中、应用信息和更新字段重叠，以及长 Package Identity 显示问题。

## 1.1.0

- 产品显示版本更新为 `1.1.0`；GitHub 旁加载包和 Microsoft Store 包均使用 `1.1.0.0`，但继续保持独立身份、Publisher 和更新流程。
- 新增 WGS 84、GCJ-02 与 BD-09 六方向点坐标本地转换；公开近似算法仅用于地图叠加、数据准备和科研辅助，不适用于测绘、审批、施工或法律用途。
- 新增本地 Shapefile 转换，支持二维 Point、MultiPoint、PolyLine 和 Polygon；明确拒绝 Z/M、NullShape 与投影坐标系，并本地化配套文件说明和状态。
- 重构设置页为分组卡片结构，语言切换后可选择立即重启或稍后重启。
- 补充 Shapefile 数据边界、第三方组件声明、隐私说明以及 GitHub / Store 双轨发布资料。

## 0.5.0 Preview（Microsoft Store 已发布）

- 当前面向用户的产品版本为 `0.5.0 Preview`。
- Microsoft Store 技术包版本为 `1.0.0.0`；该值仅用于 Store 包版本管理，不取代产品版本。
- Store 产品页：https://apps.microsoft.com/detail/9MWDPJG1BHKW
- Store 与 GitHub 旁加载渠道继续使用独立身份、包版本和更新流程。

## 0.4.1（预览版，随 v0.4.3 发布）

- 新增离线流程审核清单工具，支持设计与科研双分类位置、稳定 ID、阶段/审核项编辑、状态统计、复制、排序及本地数据备份恢复。
- 新增 `WorkflowReviewChecklistSchemaVersion = 1`，复用统一原子 JSON 存储和 `BackupFormatVersion = 1`。
- 扩展工具注册模型以支持多个分类展示位置，搜索、收藏和导航仍按唯一 Stable ID 工作。

## 0.3.9（预览版，已发布）

### Added

- 新增稳定项目类别 `design` 与 `research`、两步式新建流程和项目主页顶部分类；既有项目保持设计项目卡片与工作台，研究项目使用独立字段、卡片内容和工作台模板。
- 新增研究项目类型 `coursework`、`thesis`、`paper`、`research-project`、`other`，以及研究领域、研究对象、研究方法的本地保存与三语界面。
- 将原欢迎主页改造为项目主页，新增 GUID 项目模型、项目工作台、待办事项、规划指标快照、工作文件夹快捷入口以及项目归档与恢复。
- 新增独立项目存储域、带 `ProjectKind` 的最小项目索引、`ProjectSchemaVersion = 3`、单项目损坏隔离以及沿用 v0.3.8 原子写入、最后有效备份和未来版本保护的项目存储服务。
- 新增 `.uptbackup` 完整本地备份；`BackupFormatVersion = 1` 清单记录文件路径、大小与 SHA-256，并拒绝路径穿越、绝对路径、重复/未列出条目、危险文件和未来格式。
- 新增设置页数据管理区域；导入采用“自动预导入安全备份后完整替换”，验证失败不修改正式数据，替换失败尝试回滚。

### Changed

- 增加真实 2→3 项目迁移：所有既有项目确定性迁移为 `design`，行政区、坐标、说明和规划要求收进 `DesignDetails`；ID、目录、时间节点、工作文件夹、归档状态和兼容保留字段不变。
- `.uptbackup` 容器继续使用兼容的 `BackupFormatVersion = 1`，导出与导入完整保留 schema 3 的两类项目；schema 2 备份在读取时迁移为设计项目。
- 一级导航为项目、搜索、设计工具、科研工具、项目归档；关于和设置保持底部入口。搜索稳定 ID、搜索页面和收藏功能继续保留，未增加“常用功能”或 Favorites 一级入口。
- 应用、MSIX、关于页和更新检查 User-Agent 统一更新为 0.3.9 / 0.3.9.0。
- 工作文件夹 token 只作为本机 FutureAccessList 授权保存；导出不含 token 和外部文件夹内容，导入后标记为需要重新选择。

### Notes

- v0.3.9 仍为预览版；不提供项目类别转换、项目全文搜索、研究文献管理、云同步、账户、在线地图、备份加密或合并导入。
- 项目主页、项目工作台、三语文案及实际视觉效果等待用户人工审核。

## 0.3.8（预览版，内部开发里程碑，已完成但不单独发布）

### Added

- 新增集中式本地应用数据路径服务，明确区分业务数据、附件、最后有效备份、缓存和诊断日志，并仅允许已注册稳定工具 ID 映射工具目录。
- 新增带整数 `schemaVersion`、UTC 保存时间和 payload 的通用 JSON 数据信封；应用显示版本与数据格式版本保持独立。
- 新增可测试的 JSON 存储服务：UTF-8 序列化、同目录临时文件写入和回读验证、替换前最后有效备份、损坏文件诊断留存及结构化读取结果。
- 新增逐版本迁移执行器，拒绝重复起始版本、跳版本、迁移缺口和未知未来版本；仅在所有迁移成功后写入新版本。
- 新增隔离临时目录测试，覆盖路径安全、读写、原子失败保护、备份恢复、并发、迁移和现有设置兼容性。

### Changed

- 当时应用、MSIX、关于页、欢迎页和更新检查 User-Agent 统一更新为 0.3.8 / 0.3.8.0；最终公开候选现已推进到 v0.3.9。
- 现有主题、精度、自动计算、语言和收藏继续使用原来的 `settings.json` 路径、键名和格式；默认路径改由集中路径服务提供，不进行破坏性迁移。
- 启动时只建立空的基础目录结构，不为当前两个工具创建虚假业务记录。

### Notes

- v0.3.8 作为本地数据路径、原子写入、备份恢复、SchemaVersion 与迁移保护的内部里程碑保留，不创建标签或 Release。
- 本版本不提供导入、导出、云同步、数据库、加密保险库或用户可见的备份/恢复界面。
- 当前两个工具尚无需要保存的业务数据，因此没有注册生产迁移步骤，也不创建全局 manifest。

## 0.3.7（预览版，已发布）

### Added

- 新增简体中文（zh-CN）、日语（ja-JP）和英语（en-US）三套 `Strings/*/Resources.resw` 界面资源，所有用户可见文案迁移至 MRT Core 资源体系。
- 新增设置页“语言”设置，提供跟随系统、简体中文、日本語和 English 四个选项；语言偏好持久化到现有本地设置文件，重启后按所选语言显示。
- 新增集中式本地化服务（`ILocalizationService`/`LocalizationService`）与语言偏好解析（`LanguagePreference`），页面与服务不再各自创建 ResourceLoader。
- 新增本地化自动测试，覆盖三套资源键一致性、空值、占位符、工具/分类/导航资源键引用、语言偏好回退、搜索语言适配、稳定工具 ID 不变和版本配置。

### Changed

- 导航、分类、工具卡片、收藏、搜索、设置、关于、欢迎页、项目归档页以及两个工具页面全部完成本地化。
- `ToolDefinition`、`ToolCategoryDefinition` 和 `PrimaryNavigationDefinition` 改为保存稳定资源键，显示文字由本地化服务按当前语言解析；稳定工具 ID、分类 ID 和导航 ID 保持不变。
- 搜索索引按当前语言解析工具名称、说明和关键词，不再只依赖固定中文。
- `Package.appxmanifest` 声明 zh-CN、ja-JP、en-US 三种语言，显示名称与描述改为资源引用；应用版本更新为 0.3.7，MSIX 版本为 0.3.7.0。
- 单位名称（长度、面积、体积）改为本地化显示名称，标准单位符号保持不变。

### Notes

- v0.3.7 仍为预览版。
- 本版本不实现无重启实时切换语言；修改语言后提示将在下次启动时生效。
- 三语文案质量和实际视觉效果等待用户人工审核。

## 0.3.6（预览版，已发布）

### Added

- 汇总 v0.3.4 的设计与科研二级分类、统一工具卡片和现有工具迁移，以及 v0.3.5 的收藏系统。
- 将“常用功能”扩展为“工具搜索”：以 `ToolRegistry` 中真实可用的工具为唯一数据源，提供紧凑列表、实时本地搜索和空状态。
- 为工具定义增加稳定的拼音排序键、首字母和搜索关键词；支持中文名称/说明、稳定 ID、完整拼音、拼音缩写和关键词匹配。
- 收藏工具在搜索结果中置顶，并与字母分组互斥；所有分组均按拼音排序键和稳定工具 ID 排序。

### Changed

- 工具搜索、分类工具卡片和工具页面复用同一收藏服务及仅含稳定工具 ID 的存储，收藏变化通过既有通知即时同步。

### Fixed

- 补齐 `Square44x44Logo` 的 DPI、目标尺寸及 unplated 资源候选，修复开发包在 Windows 任务栏中可能解析为灰色占位图标的问题。

### Notes

- v0.3.4 与 v0.3.5 是内部开发里程碑，不单独发布；v0.3.6 仍处于 0.x 预览阶段。

## 0.3.5（预览版，内部开发里程碑）

### Added

- 新增仅保存稳定工具 ID 的本地收藏服务，支持添加、取消、切换、恢复和实时变化通知。
- 工具卡片与两个现有工具页面新增统一收藏入口；常用功能页显示真实收藏并即时刷新。

### Notes

- 收藏保存在现有本地设置文件中，不包含名称、页面类型或工具定义副本。
- 本里程碑不单独发布；最终公开候选版本将继续推进至 v0.3.6。

## 0.3.4（预览版，内部开发里程碑）

### Added

- 新增设计工具和科研工具的页面内二级分类浏览，以及统一、可复用的工具卡片。
- 空分类通过真实注册表筛选结果显示统一空状态。

### Changed

- 将规划指标快速计算器迁入“总体设计”，将单位与比例尺换算器迁入“详细设计”。
- 工具卡片直接使用 `ToolDefinition` 元数据，并通过稳定工具 ID、`ToolRegistry` 和 `ToolNavigation` 打开，为后续收藏功能建立统一入口。

### Notes

- 本版本不实现收藏、最近使用、新工具或项目归档数据功能。
- `v0.3.4` 仍为预览版。

## 0.3.3（预览版，已发布）

### Added

- 新增常用功能、设计工具、科研工具和项目归档顶层页面。
- 新增使用稳定内部 ID 的一级路由映射；显示文字不参与页面解析。

### Changed

- 将左侧主体导航重构为欢迎页面、常用功能、设计工具、科研工具和项目归档五个一级入口；关于和内置设置入口继续位于底部。
- 将两个现有工具从一级导航移入设计工具页面；页面按一级分类从 `ToolRegistry` 读取已上线工具，并继续通过稳定工具 ID 和 `ToolNavigation` 打开。

### Notes

- 当前空页面仅用于建立长期导航结构；本版本不实现二级分类、工具卡片、收藏数据或项目归档数据。
- `v0.3.3` 仍为预览版。

## 0.3.2（预览版，已发布）

### Added

- 新增统一、不可变的工具信息模型与稳定工具 ID。
- 新增设计工具和科研工具的一级、二级分类定义。
- 新增支持稳定排序、按 ID 安全查找、分类筛选和重复 ID 检测的工具注册表。

### Changed

- 将现有规划指标快速计算器和单位与比例尺换算器接入统一注册表与导航解析。
- 为后续分类页面、工具卡片和仅保存工具 ID 的收藏功能建立基础；本版本不实现这些 UI 或收藏功能。

### Notes

- `v0.3.2` 仍为预览版，没有改变现有整体导航 UI。

## 0.3.1（预览版，已发布）

### Changed

- 接入 UrbanPlanToolbox 应用图标资源：MSIX 大小磁贴、应用列表/开始菜单目标尺寸、商店 Logo 和窗口图标均改用提供的图标资源；移除对默认宽磁贴和启动屏模板资源的引用。
- 移除主体导航区重复的“设置”入口；“关于”和“设置”均固定在导航底部，底部设置入口继续打开原有设置页面及其主题、显示精度和自动计算功能。
- 关于页和应用内版本说明更新为 `v0.3.1 预览版`。

### Notes

- `v0.3.1` 是预览版；GitHub 发布策略见 [docs/ROADMAP.md](docs/ROADMAP.md)。

## 0.3.0（未发布）

### Added

- 关于页面新增可点击的 GitHub 项目仓库链接。
- 新增基于 GitHub Releases 的手动更新检查；发现新版本时可前往对应 Release 页面。

### Notes

- 更新检查不会自动下载、安装或静默更新应用。

## 0.2.0（未发布）

### Added

- 新增规划建筑单位换算，支持公制、中国亩、日本坪及英美建筑单位。
- 新增长度、面积和几何体积换算，以及比例尺双向换算。
- 新增输入验证和单元测试。

### Notes

- 亩、尺、坪、反和町属于地区性或传统单位；日本“畳/帖”因面积标准存在差异，不提供固定换算。
- 英美几何长度、面积和体积采用国际英尺定义，不包含 U.S. survey foot 或液体容量单位。

## 0.1.1

### Changed

- 增加双击 CMD 安装与卸载入口，并自动请求管理员权限。
- 将 MSIX、CER 和 Windows App Runtime 依赖隐藏在 `payload` 目录，降低直接双击 MSIX 的误操作风险。
- 安装前检测兼容的 Windows App Runtime，避免重复安装共享运行库。
- 安装失败时仅回滚本次导入的准确测试证书。
- 修复 x64 平台默认 Runtime Identifier 映射，确保 `Platform=x64` 默认生成 `win-x64`。

### Validation

- 人工验收通过：双击 CMD 安装/卸载、UAC 提升、已有兼容 Runtime 时仅安装主 MSIX、包身份启动、基础页面功能和准确测试证书清理。
- 尚未验证：UAC 取消、缺少兼容 Runtime 时的依赖回退，以及干净虚拟机或 Windows Sandbox 安装。

## 0.1.0

### Added

- 创建 WinUI 3 城市规划辅助工具箱基础框架。
- 增加规划指标快速计算器及建筑面积联动。
- 支持容积率、建筑密度、绿地率、人口/户均、停车位和公共服务设施配比。
- 支持主题设置、复制结果和单项目 MSIX 配置。
- 完成人工 UI 验收；桌面自动化首页入口兼容性问题不阻塞发布。
## v0.3.11.1

### 已知限制

- 已确认 Windows 原生 SplashScreen 资源与最终 MSIX 一致；但部分冷启动环境在原生启动页结束后仍可能短暂显示带标题栏的纯黑窗口。该呈现问题已记录并暂缓，不影响已验收的 Windows 本地通知功能，后续版本单独处理。

### Added

- 活动项目的未来重要时间节点会同步为 Windows 本地计划通知；有具体时间时按该时间提醒，只有日期时默认于当天 09:00 提醒。
- 计划通知会在应用启动、添加、编辑、归档或删除时间节点后刷新；不会修改 ProjectSchemaVersion 或 BackupFormatVersion。

### Fixed

- 原生启动页使用正式城市规划图标，并提供 100% 至 400% 缩放资源。

## v0.3.11

### Changed

- Windows/MSIX native splash screen reuses the existing official logo; no in-app splash or artificial startup delay.
- Shared page spacing, card actions, status badges, and less-truncated long tool text.
- ProjectSchemaVersion remains 3 and BackupFormatVersion remains 1.

## 0.4.0（预览版，已发布）

### Added

- 新增“色卡方案记录器”（`color-palette-recorder`）：独立工具数据域、稳定工具 ID、三语界面和响应式方案卡片。
- 方案支持色系、颜色类型、多个独立颜色、HEX/RGB 编辑、首张图片封面及应用内部多图附件；不修改用户上传图片源文件。
- 色卡方案及其受管附件已纳入 `.uptbackup` 导入导出闭环，并使用独立 `ToolSchemaVersion` 与现有原子写入、恢复和未来版本保护。
- 将色卡方案记录器固化为后续新增工具的首个真实模板实现，覆盖稳定 ID、独立存储、附件、三语、响应式、可访问性、备份与验收要求。

### Changed

- 颜色编辑统一为单一 HEX 输入和完整 ColorPicker；颜色用途统一命名为“颜色类型 / 色の用途 / Color role”，兼容既有颜色名称数据。

### Notes

- v0.4.0 已通过 PR #17 合并并正式发布（tag `v0.4.0`）。
- 已知限制：自 v0.3.11.1 延续，部分冷启动环境在原生 SplashScreen 结束后仍可能短暂显示带标题栏的纯黑窗口；该呈现问题不影响本版本已验收功能，后续版本单独处理。
## 0.4.3（预览版，已发布）

- 新增 `design-concept-dictionary` 设计理念词典，支持离线新增、编辑、重置、复制、删除、搜索、项目类型/标签筛选和排序。
- 使用独立 `DesignConceptDictionarySchemaVersion = 1`，数据位于 `data/tools/design-concept-dictionary/concepts.json`，复用原子保存、最后有效备份和未来版本保护。
- 将词典数据纳入现有 `.uptbackup` 校验与导入回滚流程；不新增附件，也不修改 `BackupFormatVersion`。
- 增加中文、日语和英文资源，以及工具注册、搜索、数据、复制和响应式页面回归测试。

## 0.4.2（随 v0.4.3 发布）

- Added the offline, read-only Architecture & Planning Regulations Index with 221 catalog entries, 20 official portals, source notes, filters, and official-link navigation.
- Source data is generated from the approved workbook at development time; the packaged app does not access Excel or online caches.
## 0.5.0（预览版，已通过 GitHub 旁加载渠道发布）

- 统一应用、程序集、MSIX、关于页、诊断信息和 GitHub 更新 User-Agent 使用 `0.5.0`；0.x 版本继续显示 Preview。
- 增加编译期 GitHub/ Microsoft Store 发行渠道隔离；Store Product ID 未配置时不生成虚假商店地址，也不调用 GitHub 更新流程。
- 增加离线可读的隐私、支持和第三方声明入口，支持复制脱敏诊断信息。
- 设置采用临时文件替换保存，并增加二次确认的本地数据清除；外部工作文件夹不在清除范围内。
- 本版本已通过 GitHub 旁加载渠道发布（tag `v0.5.0`）；旁加载 MSIX 技术包版本为 `0.5.0.3`，Store 技术包版本 `1.0.0.0` 不受本轮影响。
