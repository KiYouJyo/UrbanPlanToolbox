# Microsoft Store 发布指南

UrbanPlanToolbox 已在 Microsoft Store 发布。普通用户入口为：

- 产品页：https://apps.microsoft.com/detail/9MWDPJG1BHKW
- Store 协议：`ms-windows-store://pdp/?productid=9MWDPJG1BHKW`
- Store ID：`9MWDPJG1BHKW`

本仓库 release 记录中的最后实际公开 Store 产品版本为 `v1.3.0`。GitHub 最新正式版本为 `v1.4.1`；v1.4.1 按发布政策跳过 Store，下一 Store 里程碑为 `v1.5.0`。Partner Center 实际状态仍是 Submission ID 和最后已发布包版本的权威来源，不在工作流中写死。

Microsoft Store 默认只在 `x.0.0` 或 `x.5.0` 产品里程碑更新。GitHub 与 Store 的最新版本可以不同；本页不把 GitHub v1.4.1 表述为 Store v1.4.1。

## Store 身份

- Identity：`JoKiy.UrbanPlanToolbox`
- Publisher：`CN=C4E4B33A-7B77-4121-897C-7D720A5471F8`
- PublisherDisplayName：`Jo Kiyō`
- Package Family Name：`JoKiy.UrbanPlanToolbox_4wdwgytaw3v2m`

Store 渠道使用 `Package.Store.appxmanifest` 和 `DistributionChannel=Store`，生成未签名的 Store `.msixupload`。Microsoft Store 在发布阶段完成正式签名和认证。

## 当前自动发布流程

正式入口为 `.github/workflows/publish-microsoft-store.yml`，只允许从 GitHub Actions 手工触发。工作流不会响应 `push`、Pull Request 或 GitHub Release。

一次正式运行依次执行：

1. 检出指定来源，并要求其解析到当时精确的 `origin/main` HEAD。
2. 从 `UrbanPlanToolbox.csproj` 推导产品版本，从两个 Manifest 验证四段包版本。
3. 验证三语更新说明，并要求输入 `PUBLISH <产品版本>`。
4. 使用固定版本的 Microsoft Store Developer CLI 登录 Partner Center。
5. 确认不存在 Pending Submission，并从 Partner Center 动态读取最后已发布 Submission 与包版本。
6. 要求新包版本严格高于最后已发布包版本。
7. 执行单元测试，在隔离 worktree 中构建 x64 MSIX Bundle Store 上传包。
8. 验证 Store Identity、Publisher、PRI、语言候选、资源比例、包唯一性和 SHA-256。
9. 把精确 `.msixupload` 文件作为 `msstore publish` 的位置参数，并使用 `--noCommit` 创建草稿。
10. 等待 Partner Center 草稿和包信息稳定，写入并回读验证 zh-CN、ja-JP、en-US 更新说明。
11. 验证目标包在草稿中唯一匹配，拒绝同版本或更高版本的意外包。
12. 提交认证前再次读取同一 Submission ID、包和更新说明。
13. 运行 `msstore submission publish`，随后按严格状态白名单确认 Submission 已离开 `PendingCommit`。

工作流摘要中的版本、Submission ID、包 SHA、WACK 状态和 Partner Center 状态均来自本次实际步骤，不应使用摘要代替 Partner Center 最终认证结果。

## WACK 与认证

当前 GitHub Actions 不运行本地 WACK，构建收据会明确记录：

- `wackExecuted: false`
- `wackResult: NotRun`

这不表示绕过 Microsoft Store 技术合规测试。Submission 进入认证后，Microsoft 会在服务器端执行技术、安全和内容合规检查。本地 WACK 是推荐的提交前预检，可在最终候选包上另行执行并保留 HTML/XML 报告；只有真实执行后才能记录为 Passed。

不要把 `wackReady`、包构建成功或草稿上传成功描述成“WACK 已通过”。

## 渠道隔离

GitHub 旁加载渠道使用 `Package.appxmanifest` 与 `CN=AppPublisher`，拥有独立的签名、安装和更新流程。不得把旁加载身份、包或更新逻辑与 Store 身份混用；旁加载版只有在用户主动检查时访问 GitHub Releases API。

Store 工作流目前要求 GitHub 与 Store Manifest 的包版本一致。这是当前项目的发布政策；如果未来需要两个渠道独立发版，应先修改版本合同和测试，而不是临时绕过校验。

## 失败与恢复原则

- 已有 Pending Submission 时，正式工作流会在上传前停止，不会覆盖未知草稿。
- 包上传后，Partner Center 数据可能需要时间才能稳定；相关脚本会轮询，而不是立即把暂时缺失判定为永久失败。
- 提交后的 `CommitFailed`、`PreProcessingFailed`、`CertificationFailed`、`PublishFailed`、`ReleaseFailed`、拒绝或取消状态都会使工作流失败。
- 未知的提交状态也会失败关闭，避免把新状态误报为成功。
- 只有在尚未尝试提交认证、Submission ID、状态、版本和文件名均精确匹配时，失败清理才允许删除临时草稿。
- 清理步骤使用 Partner Center 当前返回的 Last Published Submission 作为动态保护对象，不使用写死的历史 ID。
- 如果提交命令已经尝试执行，不得自动删除草稿或 Submission，应先进行只读诊断。

GitHub Actions 的重新运行不是天然幂等。若运行在上传后异常中断，应先查看 Partner Center 是否已有 Pending Submission，再决定恢复或清理；不要直接重复上传同一版本。

## 版本与包要求

- `UrbanPlanToolbox.csproj` 的 `<Version>` 使用 `major.minor.patch`。
- Store 包版本由产品版本派生为 `major.minor.patch.0`。
- `Package.appxmanifest` 与 `Package.Store.appxmanifest` 必须与派生版本一致。
- 新 Store 包版本必须严格高于 Partner Center 最后已发布包版本。
- 不得为已发布的同一版本重新构建并上传不同内容。
- Store 更新继续使用 x64 MSIX Bundle，除非先完成架构政策、Partner Center 包历史和测试的系统性调整。

## 凭据与敏感文件

所需 GitHub Secrets：

- `AZURE_AD_TENANT_ID`
- `SELLER_ID`
- `AZURE_AD_APPLICATION_CLIENT_ID`
- `AZURE_AD_APPLICATION_SECRET`

不得提交：

- `.pfx`、`.p12`、`.cer`、`.key`；
- Client Secret、证书密码或访问令牌；
- 本机验收文件、用户数据或调试导出；
- GitHub 旁加载签名证书与 Store 包的混合产物。

## 应用能力与公开链接

`runFullTrust` 用于 WinUI 桌面应用所需的完全信任执行能力，以及本地文件、提醒和应用数据操作。它不代表应用会自动上传用户数据。

- 项目主页：https://github.com/KiYouJyo/UrbanPlanToolbox
- 问题反馈：https://github.com/KiYouJyo/UrbanPlanToolbox/issues
- 网站：https://kiyoujyo.github.io/UrbanPlanToolbox/
- 隐私政策：https://kiyoujyo.github.io/UrbanPlanToolbox/privacy/
- 支持页面：https://kiyoujyo.github.io/UrbanPlanToolbox/support/

Partner Center 中的应用名称、描述、隐私政策、支持网址、发布选项和 Store listing 字段应与公开网站及本次发布合同保持一致。不要在自动化运行期间同时从 Partner Center 网页手工修改同一草稿。
