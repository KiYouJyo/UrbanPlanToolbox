# 版本中立的自签名测试安装包脚本

## GitHub one-click bootstrap

`New-GitHubOneClickInstallerPackage.ps1` 生成的是轻量在线 Bootstrap，不是自包含离线安装包。它只携带安装/卸载脚本、metadata、公开 CER 和校验清单，不携带 MSIXBundle 或 `.appinstaller`。安装时由 Bootstrap 从固定 GitHub Pages 地址获取并校验 `.appinstaller`，再通过 `Add-AppxPackage -AppInstallerFile` 部署 Pages 清单指向的 GitHub Release bundle。

正式 GitHub 分发结构为：

```text
GitHub Release: one-click ZIP + MSIXBundle + SHA256SUMS
GitHub Pages:   UrbanPlanToolbox.appinstaller
```

本目录只保存脚本和模板，不保存证书、MSIX、依赖、ZIP、日志或构建输出。`New-PreviewInstallerPackage.ps1` 需要显式传入 `DisplayVersion` 与 `PackageVersion`；两者必须分别是三段和四段版本，且必须与实际 MSIX manifest 一致。

打包脚本从输入 MSIX 读取身份、Publisher、架构和版本，生成 `payload/InstallerMetadata.json`。安装、卸载和布局验证都从该文件读取 MSIX/CER 文件名和包身份，不依赖固定版本字符串。

示例（仅示例，不是脚本硬编码）：

```powershell
.\packaging\New-PreviewInstallerPackage.ps1 `
  -SignedMsixPath 'D:\release-input\UrbanPlanToolbox_0.2.0.0_x64_framework-dependent_self-signed.msix' `
  -PublicCertificatePath 'D:\release-input\UrbanPlanToolbox-v0.2.0-Framework-Dependent-Preview-Test.cer' `
  -WindowsAppRuntimeDependencyPath 'D:\release-input\Microsoft.WindowsAppRuntime.2.msix' `
  -DisplayVersion '0.2.0' -PackageVersion '0.2.0.0' `
  -OutputDirectory 'D:\release-output'
```

使用 `Test-PreviewInstallerLayout.ps1` 验证输出。脚本拒绝私钥 CER、PFX/P12、仓库内输出、版本/manifest 不一致和不安全的 payload 文件名。不得创建或保存 PFX/P12；最终发行资产必须从准确标签重新构建。
