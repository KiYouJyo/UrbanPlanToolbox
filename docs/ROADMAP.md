# 路线图与版本政策

## v1.5.1 当前发布

- 新增坐标点批量格式转换器，支持 DD、DDM、DMS 的批量识别、转换、检查和 CSV 导出。
- GitHub：正式发布 v1.5.1。
- Microsoft Store：本版本按一次性例外提交认证；认证完成前不表示已公开。
- 后续 Store 更新恢复 `x.0.0` / `x.5.0` 里程碑政策。

## 当前定位

UrbanPlanToolbox 是面向城乡规划、建筑设计与空间研究的离线优先 Windows 工具箱。核心项目、工具数据和备份保存在本机，面向 x64 Windows 用户提供 Microsoft Store 与 GitHub 旁加载两条独立渠道。

## 当前发布状态

- GitHub 最新正式版本：`v1.5.1`。
- Microsoft Store：v1.5.1 按一次性例外提交认证，认证完成并确认公开前不标记为已公开；最后实际公开版本仍以 Partner Center 状态为准。

## v1.4.2

- 发布“调研照片整理器”，用于规划、设计与 GIS 实地调研照片的 EXIF/GPS 读取、标签备注整理、统一命名和 GIS/CSV 导出。
- 状态：Released（GitHub）。
- Microsoft Store：按里程碑政策跳过本版本。
- 下一 Microsoft Store 里程碑：`v1.5.0`。

## v1.4.1

- 新增中日英规划术语库：140 条核心术语、中文/日文/英文三语、中文/日本/通用分类。
- 支持多语言搜索与别名检索、术语关系、易混淆概念辨析和来源信息。
- 更新设计工具 → 前期分析、科研工具 → 前期工具入口，并完成响应式 UI 修复。
- 宽窗口双栏底部小幅不齐列为 deferred。
- 下一 Microsoft Store 里程碑：`v1.5.0`。

## 已完成方向

v1.4.0 完成了 Tool Registry、数据模式与迁移、导入导出、设置与发行渠道、日志诊断、启动管线以及 About/首次启动向导的 Foundation 更新。Splash Logo 的后续优化仍是延期事项。

## 版本政策

- 每个获准的正式版本都可以发布到 GitHub；GitHub 可按功能与修正更频繁发布。
- Microsoft Store 默认只在产品版本为 `major.minor.0` 且 `minor` 为 `0` 或 `5` 时更新，即 `x.0.0` 或 `x.5.0` 里程碑。
- GitHub 最新版本和 Microsoft Store 当前版本可以不同；公开文档必须分别标明两者，不使用含义不清的“当前公开版本”。
- 两条渠道使用独立 Identity、Publisher、包版本、签名和更新流程；GitHub 旁加载包不能更新 Store 安装，Store 也不能更新旁加载安装。
- 普通用户优先使用 Store 稳定里程碑；需要最新正式功能时使用 GitHub Release。
- 路线图不擅自确定新的功能范围、版本号或发布日期；未标注为已完成的项目都不应被理解为已实现。
