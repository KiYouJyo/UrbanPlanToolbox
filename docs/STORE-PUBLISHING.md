# Microsoft Store 发布指南

UrbanPlanToolbox 已在 Microsoft Store 发布。普通用户入口为：

- 产品页：https://apps.microsoft.com/detail/9MWDPJG1BHKW
- Store 协议：`ms-windows-store://pdp/?productid=9MWDPJG1BHKW`
- Store ID：`9MWDPJG1BHKW`
- 当前产品版本：`1.1.0`
- 本次待人工提交技术包版本：`1.1.0.0`

## Store 身份

- Identity：`JoKiy.UrbanPlanToolbox`
- Publisher：`CN=C4E4B33A-7B77-4121-897C-7D720A5471F8`
- PublisherDisplayName：`Jo Kiyō`
- Package Family Name：`JoKiy.UrbanPlanToolbox_4wdwgytaw3v2m`

Store 渠道使用 `Package.Store.appxmanifest` 和 `DistributionChannel=Store`，生成 `.msixupload` 并在最终主线产物上运行 WACK。发布后的更新由 Microsoft Store 管理。

## v1.1.0 人工提交边界

本次从最终 `main` 构建 `1.1.0.0` `.msixupload`，完成 WACK 并生成 Partner Center 人工提交清单。截图、提交说明与已有 `PendingCommit` 必须由维护者在 Partner Center 中确认：不运行 Store CLI、Actions draft/publish 或任何自动上传，不新增、修改、提交或删除现有 `PendingCommit`。在 Microsoft 完成认证前，公开 Store 页面仍可能提供旧版本。

## 渠道隔离

GitHub 旁加载渠道使用 `Package.appxmanifest` 与 `CN=AppPublisher`，拥有独立的签名、安装和更新流程。不得把旁加载身份、包或更新逻辑与 Store 身份混用；旁加载版只有在用户主动检查时访问 GitHub Releases API。

## 应用能力与公开链接

`runFullTrust` 用于 WinUI 桌面应用所需的完全信任执行能力，以及本地文件、提醒和应用数据操作。它不代表应用会自动上传用户数据。

- 项目主页：https://github.com/KiYouJyo/UrbanPlanToolbox
- 问题反馈：https://github.com/KiYouJyo/UrbanPlanToolbox/issues
- 网站：https://kiyoujyo.github.io/UrbanPlanToolbox/
- 隐私政策：https://kiyoujyo.github.io/UrbanPlanToolbox/privacy/
- 支持页面：https://kiyoujyo.github.io/UrbanPlanToolbox/support/

## 后续更新流程

从最终 `main` 重新构建，核对正式 Identity、Publisher、资源、版本和 SHA-256；对最终 Store 产物运行 WACK，再由维护者手工上传 Partner Center。Store 包版本必须单调递增，产品显示版本与公开文档保持一致。不要为同一已发布版本复用或随意重建包。

Partner Center 中的应用名称、描述、隐私政策、支持网址和 Store listing 字段应与公开网站保持一致。不要把 GitHub 旁加载包上传为 Store 包，也不要提交私钥、证书密码或本机验收文件。
