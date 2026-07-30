[CmdletBinding()]
param(
    [switch]$RemoveTestCertificate
)

$ErrorActionPreference = 'Stop'
$payloadRoot = $PSScriptRoot
$logDirectory = Join-Path $env:LOCALAPPDATA 'UrbanPlanToolbox\Logs'
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
$logPath = Join-Path $logDirectory ("Uninstall-{0:yyyyMMdd-HHmmss}.log" -f (Get-Date))

function Write-UninstallLog([string]$Message) {
    $line = "{0:u} {1}" -f (Get-Date), $Message
    $line | Tee-Object -FilePath $logPath -Append
}

function Get-MsixMetadata([string]$MsixPath) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($MsixPath)
    try {
        $entry = $archive.Entries | Where-Object { $_.FullName -ieq 'AppxManifest.xml' } | Select-Object -First 1
        if ($null -eq $entry) { throw 'MSIX 中缺少 AppxManifest.xml。' }
        $reader = New-Object System.IO.StreamReader($entry.Open())
        try { [xml]$xml = $reader.ReadToEnd() } finally { $reader.Dispose() }
    }
    finally { $archive.Dispose() }
    $identity = $xml.SelectSingleNode("/*[local-name()='Package']/*[local-name()='Identity']")
    if ($null -eq $identity) { throw 'MSIX 缺少 Identity。' }
    [pscustomobject]@{ Name=$identity.Name; Publisher=$identity.Publisher }
}

try {
    $msixPath = Join-Path $payloadRoot 'UrbanPlanToolbox_0.1.1.0_x64_framework-dependent_self-signed.msix'
    $cerPath = Join-Path $payloadRoot 'UrbanPlanToolbox-v0.1.1-Framework-Dependent-Preview-Test.cer'
    if (-not (Test-Path -LiteralPath $msixPath -PathType Leaf)) { throw '找不到主 MSIX，无法确定准确包身份。' }
    if (-not (Test-Path -LiteralPath $cerPath -PathType Leaf)) { throw '找不到测试 CER，无法确定准确证书指纹。' }
    $metadata = Get-MsixMetadata $msixPath
    $certificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($cerPath)
    if ($certificate.HasPrivateKey -or $certificate.Subject -cne $metadata.Publisher) { throw 'CER 与该 MSIX 的发布者不匹配或包含私钥。' }
    $thumbprint = $certificate.Thumbprint.ToUpperInvariant()

    $packages = @(Get-AppxPackage -AllUsers -Name $metadata.Name | Where-Object { $_.Publisher -eq $metadata.Publisher })
    foreach ($package in $packages) {
        Write-UninstallLog "卸载 $($package.PackageFullName)。"
        Remove-AppxPackage -Package $package.PackageFullName -AllUsers
    }
    if ($RemoveTestCertificate) {
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
