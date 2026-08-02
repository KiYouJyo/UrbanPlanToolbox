# Microsoft Store 首次提交准备

当前产品显示版本为 `0.5.0 Preview`。GitHub 旁加载包和 Microsoft Store 包使用独立的身份、包版本和更新渠道。

## Store 身份

- Identity Name：`JoKiy.UrbanPlanToolbox`
- Identity Publisher：`CN=C4E4B33A-7B77-4121-897C-7D720A5471F8`
- PublisherDisplayName：`Jo Kiyō`
- 首次候选包技术版本：`1.0.0.0`
- Store 产品 ID：`9MWDPJG1BHKW`

`PublisherDisplayName` 中的 `ō` 是产品身份资料的一部分，必须保持原字符。MSA App ID、Package SID 和 Partner Center 内部信息不写入源代码、清单或应用界面。

## 渠道与更新

`DistributionChannel=GitHub` 使用 `Package.appxmanifest`、`CN=AppPublisher` 和 GitHub 更新检查逻辑。`DistributionChannel=Store` 使用 `Package.Store.appxmanifest`、上述 Store 身份和 Store 产品入口；该渠道不调用 GitHub 更新服务。Store 入口仅在用户点击后打开 `ms-windows-store://pdp/?productid=9MWDPJG1BHKW`，协议启动失败时回退到产品 HTTPS 页面。

## 能力说明

应用保留 `runFullTrust`，因为 WinUI 3 桌面应用需要以桌面进程身份启动，并使用 Windows App SDK 的桌面运行时、系统通知 COM 激活和本地文件/用户数据能力。应用不以此能力执行后台常驻、服务安装、驱动安装或远程代码加载；用户数据和规划输入仍在本机处理。

正式提交前应在 Partner Center 完成身份确认、公开隐私政策 URL、Store 元数据和 WACK。当前仓库未发现可供提交的公共 HTTPS 隐私政策 URL；这是当前 Partner Center 提交阻断项，不能用仓库内离线 `PRIVACY.md` 冒充公开 URL。当前仓库仅生成 Store 候选包，不代表已经上传或提交 Microsoft Store。

可选的 Partner Center 粘贴文本位于 `docs/store/PRIVACY-PARTNER-CENTER.md`。如果选择公开页面，静态源码位于 `docs/privacy/index.html` 和 `docs/support/index.html`；GitHub Pages 尚未启用，部署时应选择仓库分支中的静态文件源，并在部署后用实际 HTTPS 地址更新 Partner Center，不能预先使用猜测的 URL。
