简体中文 | [日本語](README.ja.md) | [English](README.en.md)

# UrbanPlanToolbox

面向城乡规划、建筑设计与空间研究的离线优先 Windows 工具箱。

[![Microsoft Store](https://img.shields.io/badge/Microsoft%20Store-Download-0078D4?logo=microsoftstore&logoColor=white)](https://apps.microsoft.com/detail/9MWDPJG1BHKW) [![CI](https://github.com/KiYouJyo/UrbanPlanToolbox/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/KiYouJyo/UrbanPlanToolbox/actions/workflows/ci.yml) ![Version](https://img.shields.io/badge/version-1.1.0-0078D4) ![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11%20x64-0078D4?logo=windows) ![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet) ![WinUI 3](https://img.shields.io/badge/UI-WinUI%203-0078D4)

## 获取应用

推荐通过 [Microsoft Store](https://apps.microsoft.com/detail/9MWDPJG1BHKW) 安装并接收更新。当前开发版本为 **1.1.0**，Microsoft Store 和 GitHub 旁加载包版本均为 `1.1.0.0`；两条渠道的身份和 Publisher 保持独立。

## 关于 UrbanPlanToolbox

UrbanPlanToolbox 是一个面向城乡规划、建筑设计和空间研究的 Windows 桌面工具箱。项目、工具数据和备份默认保存在本机，不要求账户、云同步或联网才能使用核心功能。

## 主要功能

- 设计项目与研究项目管理、项目主页、工作台、归档与恢复。
- 项目时间节点与 Windows 本地提醒，以及工作文件夹入口。
- 规划指标快速计算器、单位与比例尺换算器。
- 色卡方案记录器、流程审核清单、建筑与规划法规索引、设计理念词典。
- 本地工具搜索与收藏；支持 `.uptbackup` 数据导出、导入和恢复。
- 简体中文、日语和英语；浅色、深色和跟随系统主题。

## 安装

Microsoft Store 是普通用户的首选渠道。仓库也保留 x64 framework-dependent 自签名旁加载包的维护流程，但其版本可能落后于 Store，不能视为与 Store 版同步的安装方式；请从 [GitHub Releases](https://github.com/KiYouJyo/UrbanPlanToolbox/releases) 阅读对应版本说明并核对校验和。

## 隐私与离线设计

Microsoft Store 版的更新由 Microsoft Store 管理。旁加载版只有在用户主动检查更新时才访问 GitHub Releases API；外部法规、支持和项目链接只会在用户点击后打开。应用不要求账户、无广告、无遥测、无追踪、无自动崩溃上传，也不会自动上传用户数据。用户项目和工具数据保存在本机。

详见 [PRIVACY.md](PRIVACY.md) 和[在线隐私政策](https://kiyoujyo.github.io/UrbanPlanToolbox/privacy/)。

## 系统要求

Windows 10 17763 或更高版本，x64。开发和构建需要 .NET 10、WinUI 3、Windows App SDK 与 Windows SDK 10.0.26100.0。

## 数据与备份

应用数据保存在本机应用数据目录。设置页支持带清单和 SHA-256 校验的 `.uptbackup` 导出、导入和恢复；工作文件夹本身不会随备份上传或复制，导入后需要重新选择。

## 语言

界面支持简体中文、日本語和 English，可在设置中选择，重启后生效。

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

本仓库当前未声明项目许可证；在许可证决定前，请不要将其描述为已授权的开源软件。依赖与外部数据来源见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。
