# 故障排除与 FAQ

本页整理常见安装、更新、项目数据和后台驻留问题。遇到无法确定的问题时，不要直接删除 `%LocalAppData%\UrbanPlanToolbox`；优先保留数据并提交 Issue。

## 安装后无法正常更新

先确认你安装的是哪一个发行渠道：

- **GitHub 旁加载版**：在“关于 → 检查更新”中走 GitHub 更新链。
- **Microsoft Store / WinGet `msstore` 版**：继续走 Microsoft Store 更新链。

两种包身份独立，不能用另一渠道的包直接覆盖升级。

如果 GitHub 更新失败，可记录界面给出的具体阶段：

- Checking
- Downloading
- Verifying
- ReadyToInstall
- Installing
- RestartRequired / Restarting
- Failed

验证阶段失败时，不建议绕过 SHA-256 或签名检查。应重新获取正式 Release 资源，并在 Issue 中附上失败状态和版本信息。

## 更新后仍显示旧版本

先彻底退出应用，包括可能存在的后台驻留实例，然后重新启动。

如果使用 Microsoft Store 版，还应确认 Store 客户端实际完成了对应产品版本的发布和安装。GitHub Release 标签与 Microsoft Store 公布版本是两个独立事实源，不应只根据其中一个判断另一渠道已经更新。

## 应用关闭后进程还在

如果开启了后台驻留 / Close-to-Tray，关闭主窗口后应用可能按设计继续驻留于通知区域。

可以：

1. 从通知区域重新打开应用；
2. 在设置中关闭后台驻留 / 登录后启动；
3. 使用通知区域提供的退出操作彻底结束驻留实例。

如果准备卸载或手动部署新包，建议先彻底退出所有实例。

## 卸载时提示应用仍在运行

v1.8.1 已针对后台驻留阻塞 MSIX 卸载做过处理。如果仍遇到问题：

1. 关闭主窗口；
2. 从通知区域退出；
3. 再执行正常 Windows / Microsoft Store 卸载；
4. 若仍可复现，提交 Issue 并注明安装渠道、版本和 Windows 版本。

## 项目无法打开或提示数据损坏

UrbanPlanToolbox 的项目存储层不会在检测到损坏时直接把原数据当作默认空项目覆盖。

可能出现的状态包括：

- `RecoveredFromBackup`
- `Corrupt`
- `UnsupportedFutureVersion`
- `MigrationFailed`
- `IoFailure`

如果应用已经从 `last-valid` 备份恢复，建议立即导出一份新的 `.uptbackup`。

如果是 `UnsupportedFutureVersion`，说明数据格式比当前应用支持的版本更新。不要使用旧版本应用覆盖保存；应升级到能够识别该 schema 的版本。

## 导入 `.uptbackup` 失败

导入会验证 manifest、文件数量和大小、SHA-256、路径、项目 schema 与备份格式版本。

失败时优先检查：

- 文件是否完整复制 / 下载；
- 是否被第三方工具重新打包或修改；
- 是否来自比当前应用更高、且不兼容的未来备份格式；
- 磁盘是否有足够空间创建预导入安全备份。

不要手动删除现有项目目录后再“试一次”。当前导入是完整替换流程，并依赖预导入安全备份保证失败可恢复。

## 恢复备份后工作文件夹打不开

这是预期行为。Windows `FutureAccessList` 授权 token 不会进入 `.uptbackup`，也不能跨电脑迁移。

恢复后重新为项目选择对应工作文件夹即可。外部工作文件夹本身没有被复制进备份包。

## WebDAV 连接失败

检查：

- 服务端 URL 与路径是否正确；
- 是否应使用 HTTPS；
- 用户名 / 密码是否有效；
- 服务端是否支持应用需要的 WebDAV 操作，尤其是用于安全提升临时归档的 `MOVE`；
- 网络代理、防火墙或服务端权限是否阻止访问。

WebDAV 密码保存在 Windows Credential Locker。不要把凭据贴进公开 Issue。

## Data Pack 更新 / 导入失败

Data Pack 在激活前会校验身份、schema、最低应用版本、路径、大小、SHA-256 和清单内容。

如果新包验证失败，保留当前有效包通常比强制覆盖更安全。可检查：

- 应用版本是否达到数据包最低要求；
- `.uptdata` 是否完整；
- 是否来自正式或可信来源；
- 文件是否被编辑、重新压缩或破坏。

旧的有效包可以用于回滚。

## 切换语言后个别文本没有更新

当前语言切换应当在运行时重建 Shell 视觉树。若只有个别页面仍显示旧语言，通常应视为本地化缺陷，而不是让用户长期依赖重启规避。

提交 Issue 时请注明：

- 原语言 → 目标语言
- 具体页面 / 控件
- 是否每次都能复现
- 当前应用版本

## 如何提交高质量 Issue

建议包含：

- UrbanPlanToolbox 版本
- 安装渠道（GitHub / Microsoft Store）
- Windows 版本
- 重现步骤
- 预期行为与实际行为
- 必要截图
- 更新 / 数据操作的具体状态或错误文字

分享日志或路径前请移除个人姓名、项目正文、凭据和不必要的本机绝对路径。

Issue：<https://github.com/KiYouJyo/UrbanPlanToolbox/issues>

## 相关页面

- [快速开始](Getting-Started.md)
- [数据、备份与隐私](Data-Backup-and-Privacy.md)
- [版本与路线图](Releases-and-Roadmap.md)