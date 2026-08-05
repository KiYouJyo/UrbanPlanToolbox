# Microsoft Store GitHub Actions 发布流程

仓库通过 `.github/workflows/publish-microsoft-store.yml` 手动构建并上传 Microsoft Store 更新。工作流使用现有的 `Package.Store.appxmanifest`、`packaging/Build-StorePackage.ps1` 和正式 Store Product ID `9MWDPJG1BHKW`。

## 前置配置

在 Partner Center 中，负责自动提交的 Microsoft Entra 应用必须已添加到开发者账户，并具备 Manager 角色。随后在仓库的 **Settings > Secrets and variables > Actions** 中创建以下 Repository secrets：

- `AZURE_AD_TENANT_ID`
- `SELLER_ID`
- `AZURE_AD_APPLICATION_CLIENT_ID`
- `AZURE_AD_APPLICATION_SECRET`

这些值必须保存为 GitHub Actions secrets，不要提交到仓库、工作流文件、日志、Issue 或 Release 中。

## 运行方式

1. 确认 `UrbanPlanToolbox.csproj` 的三段版本与 `Package.Store.appxmanifest` 的四段版本一致，例如 `1.3.0` 对应 `1.3.0.0`。
2. 将发布提交合并到 `main`。工作流拒绝打包尚未进入 `main` 的提交。
3. 打开仓库的 **Actions > Publish Microsoft Store update > Run workflow**。
4. `source_ref` 可填写 `main`、已合并提交的 SHA，或指向该提交的 tag。
5. 首次验证时保持 `submit_for_certification` 为 `false`。这会上传包并保留 Partner Center 草稿，但不会送审。
6. 草稿验证无误后，再次运行并将 `submit_for_certification` 设为 `true`，即可上传并提交认证。

## 工作流执行内容

- 从所选 ref 检出源码，并确认其提交已包含在 `origin/main` 中。
- 校验项目版本、Store manifest 版本、Store Identity 和 Publisher。
- 运行 Release x64 单元测试。
- 生成未本地签名的 Store `.msixupload` bundle；Microsoft Store 在后续流程中处理签名。
- 校验包内 PRI、语言资源、架构和 Identity，并计算 SHA-256。
- 将 `.msixupload` 保存为 30 天的 GitHub Actions artifact。
- 使用 Microsoft Store Developer CLI 验证 Product ID 访问权限并上传包。
- 默认使用 `--noCommit` 创建草稿；仅在明确勾选后提交认证。

## 重要限制

- GitHub Actions 成功只表示包已上传或已提交到 Partner Center，不代表已经通过 Microsoft Store 认证或已经向用户发布。
- Microsoft Store Developer CLI 当前只支持为免费产品执行此类应用更新。
- 同一产品若已有未完成的草稿或认证中的提交，新的自动提交可能会失败；应先在 Partner Center 检查当前提交状态。
- Store 版本号必须高于已发布版本，且 Store manifest、项目版本和实际生成包必须一致。
- 不要把客户端密钥改成普通变量，也不要在排错日志中输出 secrets。

## 首次验收建议

首次运行先创建草稿，核对以下内容后再送审：

- Product ID 和 Store Identity 正确。
- 包类型仍为 MSIX Bundle，主架构为 x64。
- 版本号高于当前 Store 版本。
- 三种语言资源和 Store 图标正常。
- Actions artifact 的 SHA-256 与工作流摘要一致。
- Partner Center 草稿中没有意外修改定价、可用性、年龄分级或商店文本。
