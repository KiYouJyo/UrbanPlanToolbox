# v0.1.1 自签名测试安装包脚本

本目录只保存可复用脚本和说明模板，不保存证书、MSIX、依赖包、ZIP、日志或构建输出。

根目录的 CMD 入口仅使用 ASCII、CRLF 和自身目录定位；它调用 payload 中的 PowerShell Launcher。Launcher 负责 UAC、等待提升子进程并返回真实退出码，同时写入 `%LOCALAPPDATA%\UrbanPlanToolbox\Logs\installer-entry.log`。`-ExecutionPolicy Bypass` 只作用于该次启动的 PowerShell 子进程，不会修改用户或系统的全局执行策略。

## 生成发行目录

在仓库外的空目录中执行下列命令。三个输入文件都必须是本次构建产生的发行文件：已签名的 x64 主 MSIX、无私钥 CER 与 Microsoft 官方 x64 Windows App Runtime 依赖。

```powershell
.\packaging\New-PreviewInstallerPackage.ps1 `
  -SignedMsixPath 'D:\release-input\UrbanPlanToolbox_0.1.1.0_x64.msix' `
  -PublicCertificatePath 'D:\release-input\UrbanPlanToolbox-v0.1.1-Preview-Test.cer' `
  -WindowsAppRuntimeDependencyPath 'D:\release-input\Microsoft.WindowsAppRuntime.2.msix' `
  -OutputDirectory 'D:\release-output'
```

脚本拒绝在仓库内写入发行目录，也拒绝含私钥的证书。生成后用下列命令检查发行结构：

```powershell
.\packaging\Test-PreviewInstallerLayout.ps1 `
  -ReleaseDirectory 'D:\release-output\UrbanPlanToolbox-v0.1.1-x64-framework-dependent-self-signed'
```

随后由发行验证流程检查 MSIX 签名、CER EKU、SHA-256、安装、包身份启动、卸载及证书清理。不要将这些产物提交 Git。

## v0.1.1 人工验收状态

已验证：双击 CMD 安装与卸载、UAC 提升、系统已有兼容 Windows App Runtime 时仅安装主 MSIX、WindowsApps 包身份启动、基础 UI 功能，以及卸载后准确测试证书清理。共享 Runtime 未被卸载。

尚未验证：用户取消 UAC、缺少兼容 Runtime 时随附依赖的回退路径，以及干净虚拟机或 Windows Sandbox 安装。人工验收样品不是最终发行资产；最终资产必须从合并后的准确提交或标签重新构建。
