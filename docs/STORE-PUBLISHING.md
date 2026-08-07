# Microsoft Store 发布指南

## 当前状态与节奏

- 产品页：<https://apps.microsoft.com/detail/9MWDPJG1BHKW>
- Store ID：`9MWDPJG1BHKW`
- 最后实际公开的 Store 产品版本：`v1.3.0`
- GitHub 最新正式版本：`v1.4.0`
- v1.4.0 Store 状态：`SKIPPED BY RELEASE POLICY`
- 下一 Store 里程碑：`v1.5.0`

Microsoft Store 默认只在 `x.0.0` 或 `x.5.0` 产品里程碑更新。GitHub 与 Store 的最新版本可以不同；本页不把 GitHub v1.4.0 表述为 Store v1.4.0。

## Store 身份

- Identity：`JoKiy.UrbanPlanToolbox`
- Publisher：`CN=C4E4B33A-7B77-4121-897C-7D720A5471F8`
- PublisherDisplayName：`Jo Kiyō`
- Package Family Name：`JoKiy.UrbanPlanToolbox_4wdwgytaw3v2m`

Store 渠道使用 `Package.Store.appxmanifest` 和 `DistributionChannel=Store`，生成 `.msixupload` 并在最终主线产物上运行 WACK。发布后的更新由 Microsoft Store 管理。

## 手动工作流边界

仅在 Store 里程碑版本取得单独授权后，才允许上传 Partner Center 草稿或送认证。上传、认证和发布阶段保持独立；在认证完成前，公开 Store 页面可能仍提供旧版本。

## 渠道隔离

GitHub 旁加载渠道使用 `Package.appxmanifest` 与 `CN=AppPublisher`，拥有独立的签名、安装和更新流程。不得把旁加载身份、包或更新逻辑与 Store 身份混用；旁加载版只有在用户主动检查时访问 GitHub Releases API。

## 应用能力与公开链接

`runFullTrust` 用于 WinUI 桌面应用所需的完全信任执行能力，以及本地文件、提醒和应用数据操作；它不代表应用会自动上传用户数据。

- 项目主页：<https://github.com/KiYouJyo/UrbanPlanToolbox>
- 问题反馈：<https://github.com/KiYouJyo/UrbanPlanToolbox/issues>
- 网站：<https://kiyoujyo.github.io/UrbanPlanToolbox/>
- 隐私政策：<https://kiyoujyo.github.io/UrbanPlanToolbox/privacy/>
- 支持页面：<https://kiyoujyo.github.io/UrbanPlanToolbox/support/>

## 后续更新流程

从最终 `main` 重新构建，核对正式 Identity、Publisher、资源、版本和 SHA-256；对最终 Store 产物运行 WACK，再由维护者手工上传 Partner Center。Store 包版本必须单调递增，产品显示版本与公开文档保持一致。不要为同一已发布版本复用或随意重建包，也不要提交私钥、证书密码或本机验收文件。
