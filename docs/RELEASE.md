# 发布指南

## v1.5.1 发布决策

- GitHub：发布正式 `v1.5.1` Release、`v1.5.1` 标签和从最终 `main` 构建的 x64 framework-dependent 自签名旁加载包。
- Microsoft Store：本版本获得一次性例外授权，使用最终主线构建 Store `.msixupload` 并提交认证；提交后状态记录为认证中，不提前宣称已公开。
- 后续 Microsoft Store 版本继续遵循 `x.0.0` 或 `x.5.0` 里程碑政策。
- 新增坐标点批量格式转换器的功能、三语资源、离线处理声明和发布说明必须与本版本实现一致。

本文档是 UrbanPlanToolbox 的可复用发布边界与检查清单。

## v1.4.2 发布决策

- GitHub：发布正式 `v1.4.2` Release、`v1.4.2` 标签和 x64 framework-dependent 自签名旁加载包。
- Microsoft Store：`SKIPPED BY RELEASE POLICY`。本版本不执行 Store CLI、Partner Center、草稿上传、认证或 listing 修改；下一 Store 里程碑为 `v1.5.0`。
- 新增“调研照片整理器”，实际能力以 README、CHANGELOG 和正式 Release Notes 为准。

## v1.4.1 发布决策

- GitHub：发布正式 `v1.4.1` Release、标签和 x64 framework-dependent 自签名旁加载包。
- Microsoft Store：`SKIPPED BY RELEASE POLICY`。本版本不执行 Store CLI、Partner Center、草稿上传、认证或 listing 修改。
- v1.4.1 新增中日英规划术语库，包含 140 条核心术语、三语检索、分类、关系辨析和来源信息。
- 宽窗口左右栏底部的小幅不齐列为 deferred，不在本版本继续修改。

## v1.4.0 发布决策（历史记录）

- GitHub：发布正式 `v1.4.0` Release、标签和 x64 framework-dependent 自签名旁加载包。
- Microsoft Store：`SKIPPED BY RELEASE POLICY`。本版本不执行 Store CLI、Partner Center、草稿上传、认证或 listing 修改；下一 Store 里程碑为 `v1.5.0`。

## 发布节奏

每个获准正式版本都可以发布到 GitHub，GitHub 可更频繁发布。Microsoft Store 默认只在 `x.0.0` 或 `x.5.0` 里程碑更新，因此 GitHub 最新版本和 Store 当前版本可以不同。文档必须明确写出渠道、产品版本、包版本和发布状态。

## 通用准备

从最终 `main` 重新构建并记录提交和 SHA-256。不要复用不同提交产生的候选包，不要将证书、私钥、PFX、MSIX 或本机验收文件提交仓库。标签、GitHub Release、Store 上传和 Store 认证是相互独立的授权步骤。

## GitHub 旁加载渠道

1. 使用 `Package.appxmanifest` 与 `CN=AppPublisher`。
2. 仅在确实维护该渠道时发布 x64 framework-dependent 自签名包。
3. 生成 MSIXBundle、安装 ZIP 和 SHA256SUMS；ZIP 内不包含私钥、PDB、源代码或本机数据。
4. GitHub 更新检查只在用户主动操作时访问 Releases API。

## Microsoft Store 渠道

仅在版本符合 Store 里程碑政策且取得单独授权时执行：使用 `Package.Store.appxmanifest`、正式 Identity `JoKiy.UrbanPlanToolbox` 和 Publisher `CN=C4E4B33A-7B77-4121-897C-7D720A5471F8`，生成 `.msixupload`，在最终主线产物上运行 WACK，再由维护者手工上传 Partner Center。Store 包版本必须单调递增，发布后的更新由 Microsoft Store 管理。

现有 Store 工作流的上传、认证和发布阶段保持独立；不要把 GitHub 旁加载包上传为 Store 包，也不要在跳过 Store 的版本中调用该工作流。

## 构建与测试

```powershell
dotnet restore UrbanPlanToolbox.slnx -p:Configuration=Debug -p:Platform=x64
dotnet test tests/UrbanPlanToolbox.Tests/UrbanPlanToolbox.Tests.csproj -c Debug -p:Platform=x64
dotnet build UrbanPlanToolbox.slnx -c Debug -p:Platform=x64 --no-restore
dotnet build UrbanPlanToolbox.slnx -c Release -p:Platform=x64 --no-restore
```

发布前还应完成三语资源键集检查、安装包渠道身份检查、Git 差异检查和人工验收。Store 渠道发布时另行完成 WACK。
