[CmdletBinding()]
param(
    [switch]$RemoveCertificate
)

$ErrorActionPreference = 'Stop'
$payloadRoot = $PSScriptRoot
. (Join-Path $payloadRoot 'InstallerMetadata.ps1')
$logDirectory = Join-Path $env:LOCALAPPDATA 'UrbanPlanToolbox\Logs'
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
$logPath = Join-Path $logDirectory ("Uninstall-{0:yyyyMMdd-HHmmss}.log" -f (Get-Date))

function Write-UninstallLog([string]$Message) {
    $line = "{0:u} {1}" -f (Get-Date), $Message
    $line | Tee-Object -FilePath $logPath -Append
}

try {
    $installerMetadata = Get-InstallerMetadata $payloadRoot
    $packages = @(Get-AppxPackage -Name '556F80C5-C4D4-452B-93B4-00DE3FA7AC29' | Where-Object { $_.Publisher -eq 'CN=AppPublisher' })
    foreach ($package in $packages) {
        Write-UninstallLog "卸载 $($package.PackageFullName)。"
        Remove-AppxPackage -Package $package.PackageFullName
    }
    if ($RemoveCertificate) {
        $cerPath = Get-SafePayloadFilePath $payloadRoot $installerMetadata.certificateFileName
        if (-not (Test-Path -LiteralPath $cerPath -PathType Leaf)) { throw '找不到测试 CER，无法确定准确证书指纹。' }
        $certificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($cerPath)
        if ($certificate.HasPrivateKey -or $certificate.Subject -cne 'CN=AppPublisher') { throw 'CER 与 GitHub 包发布者不匹配或包含私钥。' }
        $thumbprint = $certificate.Thumbprint.ToUpperInvariant()
        $certificatePath = "Cert:\LocalMachine\TrustedPeople\$thumbprint"
        if (Test-Path -LiteralPath $certificatePath) {
            Remove-Item -LiteralPath $certificatePath
            Write-UninstallLog "已删除准确测试证书：$thumbprint。"
        }
        else { Write-UninstallLog "准确测试证书不在 LocalMachine TrustedPeople 中。" }
    }
    Write-UninstallLog '卸载完成；未修改共享 Windows App Runtime。'
    exit 0
}
catch {
    Write-UninstallLog "卸载失败：$($_.Exception.Message)"
    Write-Error $_
    exit 1
}
finally {
    Write-Output "UNINSTALL_LOG_PATH=$logPath"
}
