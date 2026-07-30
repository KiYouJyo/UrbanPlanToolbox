[CmdletBinding()]
param(
    [switch]$LaunchAfterInstall
)

$ErrorActionPreference = 'Stop'
$payloadRoot = $PSScriptRoot
. (Join-Path $payloadRoot 'InstallerMetadata.ps1')
$logDirectory = Join-Path $env:LOCALAPPDATA 'UrbanPlanToolbox\Logs'
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
$logPath = Join-Path $logDirectory ("Install-{0:yyyyMMdd-HHmmss}.log" -f (Get-Date))
$certificateWasImported = $false
$certificateThumbprint = $null

function Write-InstallLog([string]$Message) {
    $line = "{0:u} {1}" -f (Get-Date), $Message
    $line | Tee-Object -FilePath $logPath -Append
}

function Get-PayloadHashMap([string]$Path) {
    $hashes = @{}
    Get-Content -LiteralPath $Path | ForEach-Object {
        if ($_ -match '^(?<hash>[A-Fa-f0-9]{64}) \*(?<name>.+)$') {
            $hashes[$matches.name.Replace('/', '\\')] = $matches.hash.ToUpperInvariant()
        }
    }
    if ($hashes.Count -eq 0) { throw 'SHA256SUMS.txt 中没有可用的 SHA-256 条目。' }
    return $hashes
}

function Assert-PayloadHash([hashtable]$HashMap, [string]$RelativePath) {
    $normalized = $RelativePath.Replace('/', '\\')
    if (-not $HashMap.ContainsKey($normalized)) { throw "校验清单缺少 $normalized。" }
    $path = Join-Path $payloadRoot $normalized
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "缺少发行文件：$normalized" }
    $actual = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToUpperInvariant()
    if ($actual -ne $HashMap[$normalized]) { throw "SHA-256 不匹配：$normalized" }
    return $path
}

function Assert-Certificate([string]$CerPath, [string]$ExpectedPublisher) {
    $certificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($CerPath)
    if ($certificate.HasPrivateKey) { throw '随附 CER 不得包含私钥。' }
    if ($certificate.Subject -cne $ExpectedPublisher) { throw "CER Subject 与 MSIX Publisher 不一致：$($certificate.Subject)" }
    $eku = @($certificate.Extensions | Where-Object { $_.Oid.Value -eq '2.5.29.37' } | ForEach-Object { $_.EnhancedKeyUsages | ForEach-Object { $_.Value } })
    if ($eku -notcontains '1.3.6.1.5.5.7.3.3') { throw 'CER 缺少代码签名 EKU (1.3.6.1.5.5.7.3.3)。' }
    if ((Get-Date) -lt $certificate.NotBefore -or (Get-Date) -gt $certificate.NotAfter) { throw '测试证书当前不在有效期内。' }
    return $certificate
}

try {
    Write-InstallLog '开始验证发行有效载荷。'
    $installerMetadata = Get-InstallerMetadata $payloadRoot
    $null = Get-SafePayloadFilePath $payloadRoot $installerMetadata.msixFileName
    $null = Get-SafePayloadFilePath $payloadRoot $installerMetadata.certificateFileName
    $hashMap = Get-PayloadHashMap (Join-Path $payloadRoot 'SHA256SUMS.txt')
    $msixRelative = $installerMetadata.msixFileName
    $cerRelative = $installerMetadata.certificateFileName
    $dependencyRelative = 'Dependencies\x64\Microsoft.WindowsAppRuntime.2.msix'
    $msixPath = Assert-PayloadHash $hashMap $msixRelative
    $cerPath = Assert-PayloadHash $hashMap $cerRelative
    $dependencyPath = Assert-PayloadHash $hashMap $dependencyRelative
    $metadata = Get-MsixPackageMetadata $msixPath
    Assert-MetadataMatchesMsix $installerMetadata $metadata
    if ($metadata.Architecture -cne 'x64') { throw "主 MSIX 架构不是 x64：$($metadata.Architecture)" }
    $certificate = Assert-Certificate $cerPath $metadata.Publisher
    $certificateThumbprint = $certificate.Thumbprint.ToUpperInvariant()
    Write-InstallLog "已验证 MSIX $($metadata.Name) $($metadata.Version)，Publisher $($metadata.Publisher)。"

    $trustedStore = 'Cert:\LocalMachine\TrustedPeople'
    $trustedCertificate = Get-ChildItem -Path $trustedStore | Where-Object { $_.Thumbprint -eq $certificateThumbprint } | Select-Object -First 1
    if ($null -eq $trustedCertificate) {
        Import-Certificate -FilePath $cerPath -CertStoreLocation $trustedStore | Out-Null
        $certificateWasImported = $true
        Write-InstallLog "已导入准确测试证书 $certificateThumbprint 到 LocalMachine TrustedPeople。"
    }
    else { Write-InstallLog "准确测试证书已在 LocalMachine TrustedPeople 中受信任。" }

    $runtime = Get-AppxPackage -AllUsers -Name 'Microsoft.WindowsAppRuntime.2' | Where-Object { $_.Architecture -eq 'X64' -and $_.Publisher -eq 'CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US' -and [version]$_.Version -ge $metadata.RuntimeMinVersion } | Sort-Object Version -Descending | Select-Object -First 1
    if ($null -ne $runtime) {
        Write-InstallLog "找到兼容 Windows App Runtime $($runtime.Version)；仅安装主 MSIX。"
        Add-AppxPackage -Path $msixPath
    }
    else {
        Write-InstallLog "未找到 $($metadata.RuntimeMinVersion) 或更高版本的兼容 x64 Windows App Runtime；使用随附依赖。"
        Add-AppxPackage -Path $msixPath -DependencyPath $dependencyPath
    }

    $installed = Get-AppxPackage -Name $metadata.Name | Where-Object { $_.Publisher -eq $metadata.Publisher -and $_.Architecture -eq 'X64' } | Sort-Object Version -Descending | Select-Object -First 1
    if ($null -eq $installed -or [version]$installed.Version -ne $metadata.Version -or $installed.Status -ne 'Ok' -or $installed.IsDevelopmentMode) { throw '安装后的包身份、版本、状态或开发模式验证失败。' }
    Write-InstallLog "安装验证通过：$($installed.PackageFullName)；Status=$($installed.Status)；IsDevelopmentMode=$($installed.IsDevelopmentMode)。"
    if ($LaunchAfterInstall) {
        $aumid = "$($installed.PackageFamilyName)!$($metadata.AppId)"
        Start-Process explorer.exe "shell:AppsFolder\$aumid"
        Write-InstallLog "已请求启动包身份应用：$aumid。"
    }
    exit 0
}
catch {
    Write-InstallLog "安装失败：$($_.Exception.Message)"
    if ($certificateWasImported -and $certificateThumbprint) {
        $certificatePath = "Cert:\LocalMachine\TrustedPeople\$certificateThumbprint"
        if (Test-Path -LiteralPath $certificatePath) {
            Remove-Item -LiteralPath $certificatePath
            Write-InstallLog "已回滚本次导入的准确测试证书：$certificateThumbprint。"
        }
    }
    Write-Error $_
    exit 1
}
finally {
    Write-Output "INSTALL_LOG_PATH=$logPath"
}
