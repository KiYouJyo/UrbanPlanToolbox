[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$SignedMsixPath,
    [Parameter(Mandatory)] [string]$PublicCertificatePath,
    [Parameter(Mandatory)] [string]$WindowsAppRuntimeDependencyPath,
    [Parameter(Mandatory)] [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$outputFullPath = [System.IO.Path]::GetFullPath($OutputDirectory)
if ($outputFullPath.StartsWith($repositoryRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw '发行目录必须位于仓库外，不能在 Git 工作区内生成发行资产。'
}
foreach ($path in @($SignedMsixPath, $PublicCertificatePath, $WindowsAppRuntimeDependencyPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "找不到输入文件：$path" }
}
if ([System.IO.Path]::GetExtension($PublicCertificatePath) -notin @('.cer', '.CER')) { throw '只接受无私钥 CER 公钥文件。' }
$cert = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new((Resolve-Path $PublicCertificatePath))
if ($cert.HasPrivateKey) { throw '拒绝包含私钥的证书文件。' }

$releaseRoot = Join-Path $outputFullPath 'UrbanPlanToolbox-v0.1.1-x64-framework-dependent-self-signed'
if (Test-Path -LiteralPath $releaseRoot) { throw "输出目录已存在，拒绝覆盖：$releaseRoot" }
$payload = Join-Path $releaseRoot 'payload'
$dependencyDestination = Join-Path $payload 'Dependencies\x64'
New-Item -ItemType Directory -Path $dependencyDestination -Force | Out-Null

Copy-Item -LiteralPath (Join-Path $PSScriptRoot '① 安装规划工具箱.cmd') -Destination $releaseRoot
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '② 卸载规划工具箱.cmd') -Destination $releaseRoot
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '请先阅读.txt') -Destination $releaseRoot
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'payload\Install.ps1') -Destination $payload
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'payload\Uninstall.ps1') -Destination $payload
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'payload\InstallLauncher.ps1') -Destination $payload
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'payload\UninstallLauncher.ps1') -Destination $payload
Copy-Item -LiteralPath $SignedMsixPath -Destination (Join-Path $payload 'UrbanPlanToolbox_0.1.1.0_x64_framework-dependent_self-signed.msix')
Copy-Item -LiteralPath $PublicCertificatePath -Destination (Join-Path $payload 'UrbanPlanToolbox-v0.1.1-Framework-Dependent-Preview-Test.cer')
Copy-Item -LiteralPath $WindowsAppRuntimeDependencyPath -Destination (Join-Path $dependencyDestination 'Microsoft.WindowsAppRuntime.2.msix')

$hashLines = Get-ChildItem -LiteralPath $payload -Recurse -File |
    Where-Object { $_.Name -ne 'SHA256SUMS.txt' } |
    Sort-Object FullName |
    ForEach-Object {
        $relativePath = $_.FullName.Substring($payload.Length).TrimStart('\')
        $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToUpperInvariant()
        "$hash *$relativePath"
    }
Set-Content -LiteralPath (Join-Path $payload 'SHA256SUMS.txt') -Value $hashLines -Encoding UTF8
Write-Output $releaseRoot
