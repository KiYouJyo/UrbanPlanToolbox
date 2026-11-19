# 快速开始

本页适合第一次安装 UrbanPlanToolbox 的用户。

## 系统要求

- Windows 10 17763 或更高版本
- x64 架构
- 不要求登录 UrbanPlanToolbox 账户
- 大部分核心工具可离线运行；更新、Data Pack 下载和 WebDAV 云归档需要在用户主动操作时联网

## 选择安装渠道

UrbanPlanToolbox 有两条正式发行链，两者**不能互相覆盖升级**。

### Microsoft Store / WinGet

适合希望交给 Microsoft Store 管理安装和更新的用户。

```powershell
winget install --id 9MWDPJG1BHKW --source msstore -e
```

通过 WinGet 的 `msstore` 源安装，本质上仍属于 Microsoft Store 渠道。

### GitHub Release

适合希望更快获得正式版本的用户。首次安装建议从 GitHub 最新 Release 下载项目提供的一键安装包；它负责完成所需证书配置，并获取当前正式 x64 安装包。

高级用户也可以直接使用 Release Assets 中的 `.msixbundle` 与 SHA-256 清单进行手动部署和校验。

## 第一次启动建议

完成首次启动后，可以按以下顺序熟悉应用：

1. 在 **设置** 中确认主题、语言、显示精度和自动计算偏好。
2. 打开 **搜索**，确认可以按当前界面语言搜索工具。
3. 新建一个 **设计项目** 或 **研究项目**，体验项目主页与工作台。
4. 在 **设计工具 / 科研工具** 中打开所需独立工具。
5. 在专业资料页面检查 **法规、规划术语、设计理念** 的 Data Pack 状态。
6. 如需随手记录想法，再开启 **后台驻留 / 灵感记录器**；这些行为均由用户控制。
7. 在 **设置 → 数据管理** 中了解 `.uptbackup` 导出以及可选 WebDAV 云归档。

## 更新应用

### GitHub 版

打开 **关于 → 检查更新**。应用的 GitHub 更新流程会经历下载、完整性校验、签名校验、安装与重启阶段。当前正式更新链会检查 SHA-256，并验证 MSIX/Authenticode 签名及固定的签名者信息。

### Microsoft Store 版

继续由 Microsoft Store 管理更新。应用内的 Store 更新交互使用 Windows 原生的下载与安装流程。

> 不要尝试用 GitHub 包覆盖 Microsoft Store 包，反之亦然。两个渠道具有独立包身份。

## Data Pack 与应用更新不是一回事

从 v1.9.2 起，法规、规划术语与设计理念的专业数据使用独立 Data Pack。它们有自己的数据版本和更新流程：

- 应用版本更新的是 UrbanPlanToolbox 本体；
- Data Pack 更新的是专业资料内容；
- 已安装 Data Pack 可离线使用；
- Data Pack 更新检查由用户主动发起；
- 旧的有效数据包可以保留用于回滚。

详见 [专业知识库与 Data Pack](Professional-Libraries.md)。

## 下一步

- [功能总览](Feature-Guide.md)
- [项目与工作台](Projects-and-Workspace.md)
- [数据、备份与隐私](Data-Backup-and-Privacy.md)
- [故障排除](Troubleshooting.md)