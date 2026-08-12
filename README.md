简体中文 | [日本語](README.ja.md) | [English](README.en.md)

# UrbanPlanToolbox

## v1.5.4 正式发布状态

v1.5.4 修复更新弹窗标题资源显示异常，改进 Microsoft Store 应用内更新进度、安装状态、生命周期和诊断日志。GitHub 已正式发布；Microsoft Store v1.5.4 的认证和公开状态以 Partner Center 及用户端可获取状态为准。

面向城乡规划、建筑设计与空间研究的离线优先 Windows 工具箱。

[![MIT License](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE) [![CI](https://github.com/KiYouJyo/UrbanPlanToolbox/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/KiYouJyo/UrbanPlanToolbox/actions/workflows/ci.yml) [![GitHub Release](https://img.shields.io/github/v/release/KiYouJyo/UrbanPlanToolbox?display_name=tag&sort=semver)](https://github.com/KiYouJyo/UrbanPlanToolbox/releases/latest) ![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20WinUI%203-0078D4?logo=windows) [![Microsoft Store](https://img.shields.io/badge/Microsoft%20Store-Download-0078D4?logo=microsoftstore&logoColor=white)](https://apps.microsoft.com/detail/9MWDPJG1BHKW)

## 获取应用

普通用户可通过 [Microsoft Store](https://apps.microsoft.com/detail/9MWDPJG1BHKW) 安装稳定版本；Store v1.5.4 是否已公开以 Partner Center 和用户端可获取状态为准。GitHub 最新正式版本为 **v1.5.4**，并可更频繁发布 x64 framework-dependent 自签名旁加载包。两条渠道的身份、Publisher 和更新流程保持独立。

## 关于 UrbanPlanToolbox

UrbanPlanToolbox 是一个面向城乡规划、建筑设计和空间研究的 Windows 桌面工具箱。项目、工具数据和备份默认保存在本机，不要求账户、云同步或联网才能使用核心功能。

## 主要功能

- 设计项目与研究项目管理、项目主页、工作台、归档与恢复。
- 项目时间节点与 Windows 本地提醒，以及工作文件夹入口。
- 规划指标快速计算器、单位与比例尺换算器。
- 色卡方案记录器、流程审核清单、建筑与规划法规索引、设计理念词典。
- 坐标系转换器支持 WGS 84、GCJ-02 与 BD-09 点坐标的本地转换，并在本机处理 Shapefile；不支持投影坐标系。
- 本地工具搜索与收藏；支持 `.uptbackup` 数据导出、导入和恢复。
- 简体中文、日语和英语；浅色、深色和跟随系统主题。

### 调研照片整理器

面向规划、设计与 GIS 实地调研的本地照片整理工具，可读取照片 EXIF/GPS 信息，整理标签与备注，并导出统一命名的照片副本、GIS 点位及 CSV 数据。

- 批量导入 JPG、JPEG、HEIC、HEIF 和 PNG 照片。
- 读取 EXIF、GPS、拍摄时间、海拔和方向信息，并提供缩略图预览。
- 使用自由输入的标签与备注整理照片。
- 导出 WGS 84 / EPSG:4326 Shapefile 点位和 CSV 元数据；无 GPS 照片不进入点图层，但仍保留在照片和 CSV 输出中。
- 原始照片只读，照片和 GPS 仅在本机处理；HEIC 预览可能依赖 Windows 图像编解码器，但不影响已支持的元数据读取。

## 安装

Microsoft Store 是普通用户获取稳定里程碑版本的首选渠道。仓库同时提供 x64 framework-dependent 自签名旁加载包；需要最新正式功能时，请从[最新 GitHub Release](https://github.com/KiYouJyo/UrbanPlanToolbox/releases/latest) 获取当前包，并在安装前核对校验和。两条渠道身份独立，不能相互覆盖升级。

## 隐私与离线设计

Microsoft Store 版的更新由 Microsoft Store 管理。旁加载版只有在用户主动检查更新时才访问 GitHub Releases API；外部法规、支持和项目链接只会在用户点击后打开。应用不要求账户、无广告、无遥测、无追踪、无自动崩溃上传，也不会自动上传用户数据。用户项目和工具数据保存在本机。

GCJ-02 与 BD-09 结果采用公开近似算法，仅适用于地图叠加、数据准备和科研辅助，不属于测绘、审批、施工或法律用途的坐标转换成果。

详见 [PRIVACY.md](PRIVACY.md) 和[在线隐私政策](https://kiyoujyo.github.io/UrbanPlanToolbox/privacy/)。

## 系统要求

Windows 10 17763 或更高版本，x64。开发和构建需要 .NET 10、WinUI 3、Windows App SDK 与 Windows SDK 10.0.26100.0。

## 数据与备份

应用数据保存在本机应用数据目录。设置页支持带清单和 SHA-256 校验的 `.uptbackup` 导出、导入和恢复；工作文件夹本身不会随备份上传或复制，导入后需要重新选择。

## 语言

界面支持简体中文、日本語和 English，可在设置中选择，立即生效。

## 文档

- [路线图与版本政策](docs/ROADMAP.md)
- [发布指南](docs/RELEASE.md)
- [Microsoft Store 发布指南](docs/STORE-PUBLISHING.md)
- [数据存储](docs/DATA_STORAGE.md)、[数据备份](docs/DATA_BACKUP.md)
- [更改日志](CHANGELOG.md)
- [第三方声明](THIRD-PARTY-NOTICES.md)

## 开发与构建

```powershell
dotnet restore UrbanPlanToolbox.slnx -p:Configuration=Debug -p:Platform=x64
dotnet test tests/UrbanPlanToolbox.Tests/UrbanPlanToolbox.Tests.csproj -c Debug -p:Platform=x64
```

完整的构建、渠道隔离、WACK 和发布流程见 [docs/RELEASE.md](docs/RELEASE.md)。

## 问题反馈

请通过 [GitHub Issues](https://github.com/KiYouJyo/UrbanPlanToolbox/issues) 反馈问题，或访问[支持页面](https://kiyoujyo.github.io/UrbanPlanToolbox/support/)。提交诊断信息前请移除本机路径、项目内容和个人数据。

## 路线图

当前完成情况与未来方向见 [docs/ROADMAP.md](docs/ROADMAP.md)。路线图用于说明方向，不构成版本或日期承诺。

## 许可证与第三方声明

UrbanPlanToolbox 使用 [MIT License](LICENSE) 开源。依赖与外部数据来源见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。

## v1.5.0 GitHub Release

- 新增“图纸版本差异对比器”，本地比较两张像素尺寸完全一致的 PNG、JPG/JPEG 或 PDF 页面图像。
- 提供半透明叠加和擦除浏览两种模式，支持保持比例、缩放、平移、适配窗口和叠加结果 PNG 导出。
- 原始文件不被覆盖；本版本仅发布 GitHub，Microsoft Store 不执行任何操作。

## v1.4.2 GitHub Release

- 新增“调研照片整理器”（设计工具 → 实地调研），面向规划、设计与 GIS 实地调研照片整理。
- 支持 EXIF/GPS、拍摄时间、海拔、方向、缩略图、Tags/标签和 Note/备注。
- 支持统一命名的照片副本、WGS 84 / EPSG:4326 Shapefile 点位和 CSV 元数据导出。
- 原始照片保持不变，照片和 GPS 仅在本机处理；v1.4.2 仅发布 GitHub，Microsoft Store 按里程碑政策跳过。

## v1.4.1 GitHub Release

- 新增中日英规划术语库：140 条核心术语，支持中文、日文、英文三语对照。
- 支持中文、日本、通用分类，多语言搜索与别名检索、术语关系、易混淆概念辨析和来源信息。
- 更新设计工具 → 前期分析、科研工具 → 前期工具入口，并完成响应式 UI 修复。
- GitHub 正式发布；Microsoft Store 不在本版本发布范围内。宽窗口双栏底部小幅不齐列为 deferred。

## v1.3.0 GitHub Release

- 支持运行时中日英语言切换，以及项目里程碑提醒设置。
- GitHub 正式发布；Microsoft Store 当时已公开的稳定版本为 v1.3.0。

<!-- Pages redeploy trigger: recover from cancelled deployment 31102137189 -->
