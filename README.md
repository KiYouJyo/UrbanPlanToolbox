简体中文 | [日本語](README.ja.md) | [English](README.en.md)

# UrbanPlanToolbox

面向城乡规划、建筑设计与空间研究的离线优先 Windows 工具箱。

[![MIT License](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE) [![CI](https://github.com/KiYouJyo/UrbanPlanToolbox/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/KiYouJyo/UrbanPlanToolbox/actions/workflows/ci.yml) [![GitHub Release](https://img.shields.io/github/v/release/KiYouJyo/UrbanPlanToolbox?display_name=tag&sort=semver)](https://github.com/KiYouJyo/UrbanPlanToolbox/releases/latest) [![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20WinUI%203-0078D4?logo=windows)](https://github.com/KiYouJyo/UrbanPlanToolbox) [![Microsoft Store](https://img.shields.io/badge/Microsoft%20Store-Download-0078D4?logo=microsoftstore&logoColor=white)](https://apps.microsoft.com/detail/9MWDPJG1BHKW)

## 获取应用

- [GitHub 最新正式 Release](https://github.com/KiYouJyo/UrbanPlanToolbox/releases/latest)：提供更新更频繁的 x64 旁加载版本。
- [Microsoft Store](https://apps.microsoft.com/detail/9MWDPJG1BHKW)：适合希望通过 Microsoft Store 安装和更新的用户。

两个渠道的身份和更新链独立，不能相互覆盖升级。版本详情和变化请查看 [GitHub Releases](https://github.com/KiYouJyo/UrbanPlanToolbox/releases) 与 [CHANGELOG.md](CHANGELOG.md)。

## 核心功能

- 设计与研究项目管理、项目主页、工作台、归档和恢复。
- 项目时间节点、本地提醒和工作文件夹入口。
- 规划指标计算、单位与比例尺换算、色卡方案、流程审核清单。
- 建筑与规划法规索引、设计理念词典和本地工具搜索收藏。
- WGS 84、GCJ-02 与 BD-09 点坐标本地转换及 Shapefile 处理。
- 调研照片整理、EXIF/GPS 读取、GIS 点位和 CSV 导出。
- 简体中文、日本語和 English；浅色、深色及跟随系统主题。

## 安装与更新

### 首次 GitHub 安装

请从 [最新 GitHub Release](https://github.com/KiYouJyo/UrbanPlanToolbox/releases/latest) 下载体积较小的一键安装包。它会完成所需的证书配置，并在线获取当前正式版本后安装应用，以便后续直接在应用内更新。

### 后续更新

在应用中打开“关于”→“检查更新”。GitHub 版本会通过 Windows App Installer 下载并安装后续版本；Microsoft Store 版本继续由 Microsoft Store 管理。

### 高级安装

高级用户可在 Release Assets 获取 `.msixbundle` 和 SHA-256 清单进行手动部署或验证；固定的 App Installer 清单由项目 Pages 地址提供。

## 隐私与离线设计

核心功能不要求账户、云同步或联网。项目、工具数据和备份默认保存在本机；更新检查只在用户主动操作时访问对应发行渠道。照片、GPS 和坐标数据在本机处理。

## 系统要求

Windows 10 17763 或更高版本，x64。

## 数据与备份

设置支持带清单和 SHA-256 校验的 `.uptbackup` 导出、导入和恢复。工作文件夹内容不会随备份上传或复制，导入后需要重新选择。

## 语言

支持简体中文、日本語和 English，可在设置中切换并立即生效。

## 文档

- [路线图与版本政策](docs/ROADMAP.md)
- [发布指南](docs/RELEASE.md)
- [数据存储](docs/DATA_STORAGE.md) · [数据备份](docs/DATA_BACKUP.md)
- [更改日志](CHANGELOG.md)
- [隐私政策](PRIVACY.md) · [第三方声明](THIRD-PARTY-NOTICES.md)

## 开发与构建

```powershell
dotnet restore UrbanPlanToolbox.slnx -p:Configuration=Debug -p:Platform=x64
dotnet test UrbanPlanToolbox.slnx -c Debug -p:Platform=x64 --no-restore
```

完整构建与发布流程见 [docs/RELEASE.md](docs/RELEASE.md)。

## 问题反馈

请通过 [GitHub Issues](https://github.com/KiYouJyo/UrbanPlanToolbox/issues) 反馈问题，或访问[支持页面](https://kiyoujyo.github.io/UrbanPlanToolbox/support/)。分享诊断信息前请移除本机路径和个人数据。

## License

项目使用 [MIT License](LICENSE) 开源。
