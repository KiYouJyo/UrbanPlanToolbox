# 发布指南

本文档是今后发布 UrbanPlanToolbox 的可复用指南。当前产品版本为 `1.2.1`；GitHub 旁加载包和 Microsoft Store 包均使用 `1.2.1.0`，但身份与更新流程相互独立。

## 产品版本

- 应用显示版本：当前为 `1.2.1`，由产品版本和应用资源共同表达。
- Store 包版本：独立、单调递增，第四段为 `0`。
- GitHub 旁加载包使用独立的 `Package.appxmanifest` 版本和签名身份。

## 通用准备

从最终 `main` 重新构建，记录提交和 SHA-256。不要复用不同提交产生的候选包，不要将证书、私钥、PFX、MSIX 或本机验收文件提交仓库。Store 上传成功后，不要为同一版本随意重建；新版本必须提高对应渠道的包版本。

## Microsoft Store 渠道

1. 使用 `Package.Store.appxmanifest`、正式 Identity `JoKiy.UrbanPlanToolbox` 和 Publisher `CN=C4E4B33A-7B77-4121-897C-7D720A5471F8`。
2. 使用 `DistributionChannel=Store` 构建；此渠道不调用 GitHub 更新。
3. 生成 `.msixupload`，在最终主线产物上运行 WACK。
4. 上传 Partner Center，完成认证和发布后由 Microsoft Store 分发与更新。

`v1.2.1` 的 Store 工作流首次支持手动上传草稿；首次运行必须使用 `submit_for_certification=false`，完成 Partner Center 草稿验收后才可再次手动送认证。GitHub Release 与 Store 提交保持独立。

## GitHub 旁加载渠道

1. 使用 `Package.appxmanifest` 与 `CN=AppPublisher`。
2. 仅在确实维护该渠道时发布 x64 framework-dependent 自签名包。
3. 使用独立的签名、安装和更新流程；旁加载身份不得与 Store 身份混用。
4. GitHub 更新检查只在用户主动操作时访问 Releases API。

## 构建与测试

```powershell
dotnet restore UrbanPlanToolbox.slnx -p:Configuration=Debug -p:Platform=x64
dotnet test tests/UrbanPlanToolbox.Tests/UrbanPlanToolbox.Tests.csproj -c Debug -p:Platform=x64
dotnet build UrbanPlanToolbox.slnx -c Debug -p:Platform=x64 --no-restore
dotnet build UrbanPlanToolbox.slnx -c Release -p:Platform=x64 --no-restore
```

发布前还应完成三语资源键集检查、安装包渠道身份检查、WACK（Store 渠道）和人工验收。不要在本流程中重新构建或上传已发布的 Store 包。

## 公开发布原则

- 从最终主线提交构建并保留构建记录和 SHA-256。
- GitHub Release 只上传经过确认的公开资产，不上传私钥或本机数据。
- 发布说明准确区分产品版本、两条渠道的包版本与独立身份，并说明 Store 认证状态。
- 发布、标签、合并和 Store 上传是相互独立的授权步骤。
