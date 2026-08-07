简体中文 | [日本語](README.ja.md) | [English](README.en.md)

# UrbanPlanToolbox

面向城乡规划、建筑设计与空间研究的离线优先 Windows 工具箱。

[![MIT License](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE) [![CI](https://github.com/KiYouJyo/UrbanPlanToolbox/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/KiYouJyo/UrbanPlanToolbox/actions/workflows/ci.yml) [![GitHub Release](https://img.shields.io/github/v/release/KiYouJyo/UrbanPlanToolbox?display_name=tag&sort=semver)](https://github.com/KiYouJyo/UrbanPlanToolbox/releases/latest) ![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20WinUI%203-0078D4?logo=windows) [![Microsoft Store](https://img.shields.io/badge/Microsoft%20Store-Download-0078D4?logo=microsoftstore&logoColor=white)](https://apps.microsoft.com/detail/9MWDPJG1BHKW)

## 获取应用

普通用户可通过 [Microsoft Store](https://apps.microsoft.com/detail/9MWDPJG1BHKW) 安装稳定里程碑版本；当前最后实际公开的 Store 版本为 **v1.3.0**。GitHub 最新正式版本为 **v1.4.0**，并可更频繁发布 x64 framework-dependent 自签名旁加载包。Microsoft Store 默认只在 `x.0.0` 或 `x.5.0` 里程碑更新，因此 v1.4.0 按本次发布政策跳过 Store，下一 Store 里程碑为 v1.5.0。两条渠道的身份、Publisher 和更新流程保持独立。

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

<!-- Pages redeploy trigger: recover from cancelled deployment 31102137189 -->
