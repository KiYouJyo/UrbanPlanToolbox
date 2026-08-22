# 贡献指南

[English](CONTRIBUTING.en.md) | [日本語](CONTRIBUTING.ja.md)

欢迎提交问题、文档改进和经过充分说明的代码变更。请先通过 Issue 说明问题或提案，再提交 PR。

## 支持范围

优先处理可复现的缺陷、隐私或数据安全问题、现有功能的文档修正，以及与当前路线图一致的改进。新工具或新数据源请先说明用户场景、数据边界、备份影响和三语文案需求。

## Issue 与 PR

Issue 应包含应用版本、安装渠道、Windows 版本、复现步骤、预期和实际结果。PR 应说明范围、验证方式和是否影响本地数据。提交前运行测试、Debug/Release x64 构建和 `git diff --check`。

## 分支与提交

从 `main` 创建主题分支，使用简洁的 Conventional Commits 风格提交信息，例如 `docs: ...`、`fix: ...` 或 `feat: ...`。不要直接修改 `main`。

## 构建和测试

```powershell
dotnet restore UrbanPlanToolbox.slnx -p:Configuration=Debug -p:Platform=x64
dotnet test tests/UrbanPlanToolbox.Tests/UrbanPlanToolbox.Tests.csproj -c Debug -p:Platform=x64
dotnet build UrbanPlanToolbox.slnx -c Release -p:Platform=x64 --no-restore
```

三语资源 `Strings/zh-CN`、`Strings/ja-JP` 和 `Strings/en-US` 必须保持键集一致。新工具应遵循 [工具开发模板](docs/TOOL_DEVELOPMENT_TEMPLATE.md)，并接入注册、导航、搜索、收藏、存储、备份和本地化契约。

## 数据与机密

不要提交证书、私钥、PFX、Token、用户数据、本机路径、MSIX 或其他构建产物。应用坚持本地优先和不自动上传用户数据的原则。

## 社区文档

- [安全政策](SECURITY.zh-CN.md)
- [行为准则](CODE_OF_CONDUCT.zh-CN.md)
