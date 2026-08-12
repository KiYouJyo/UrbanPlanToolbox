[CmdletBinding()]
param([switch]$LaunchAfterInstall, [switch]$ImportCertificateOnly, [string]$BundlePathOverride)
$ErrorActionPreference = 'Stop'
$payloadRoot = $PSScriptRoot
. (Join-Path $payloadRoot 'InstallerMetadata.ps1')
. (Join-Path $payloadRoot 'ChecksumResolver.ps1')
. (Join-Path $payloadRoot 'ReleaseDownloadResolver.ps1')
$logDirectory = Join-Path $env:LOCALAPPDATA 'UrbanPlanToolbox\Logs'
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
$logPath = Join-Path $logDirectory ("Install-{0:yyyyMMdd-HHmmss}.log" -f (Get-Date))
function Log([string]$Message) { "{0:u} {1}" -f (Get-Date), $Message | Tee-Object -FilePath $logPath -Append }
function Is-Administrator { $p = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent()); $p.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator) }
function Invoke-ReleaseMetadataWithRetry([string]$Uri, [hashtable]$Headers, [scriptblock]$Log) {
    for ($attempt = 1; $attempt -le 3; $attempt++) {
        try { return Invoke-RestMethod -Uri $Uri -Headers $Headers -Method Get -ErrorAction Stop }
        catch {
            & $Log "Release metadata failed: Attempt=$attempt/3; ExceptionType=$($_.Exception.GetType().FullName); HRESULT=$($_.Exception.HResult); Message=$($_.Exception.Message)"
            if ($attempt -ge 3 -or -not (Test-TransientNetworkException $_.Exception)) { throw 'ReleaseMetadataFailed' }
            Start-Sleep -Seconds (@(2, 5, 10))[[Math]::Min($attempt - 1, 2)]
        }
    }
}
try {
    $metadata = Get-InstallerMetadata $payloadRoot
    $hashMap = @{}
    Get-Content -LiteralPath (Join-Path $payloadRoot 'SHA256SUMS.txt') | ForEach-Object { if ($_ -match '^(?<hash>[A-Fa-f0-9]{64}) \*(?<name>.+)$') { $hashMap[$matches.name.Replace('/','\')] = $matches.hash.ToUpperInvariant() } }
    foreach ($name in @($metadata.certificateFileName, 'SHA256SUMS.txt')) {
        $path = Get-SafePayloadFilePath $payloadRoot $name
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing payload file: $name" }
        if ($name -ne 'SHA256SUMS.txt' -and $hashMap[$name] -ne (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToUpperInvariant()) { throw "SHA-256 mismatch: $name" }
    }
    $certPath = Get-SafePayloadFilePath $payloadRoot $metadata.certificateFileName
    $certificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new($certPath)
    if ($certificate.HasPrivateKey -or $certificate.Subject -cne $metadata.publisher) { throw 'Certificate publisher mismatch.' }
    $thumbprint = $certificate.Thumbprint.ToUpperInvariant()
    Log "Validated $($metadata.displayVersion), Release=$($metadata.releaseTag), Publisher=$($metadata.publisher), Thumbprint=$thumbprint."
    $trusted = Get-ChildItem 'Cert:\LocalMachine\TrustedPeople' -ErrorAction SilentlyContinue | Where-Object Thumbprint -eq $thumbprint
    if (-not $trusted -and $ImportCertificateOnly) {
        if (-not (Is-Administrator)) { throw 'Certificate trust requires elevation.' }
        Import-Certificate -FilePath $certPath -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople' | Out-Null
        Log 'Imported the public certificate into LocalMachine TrustedPeople.'; exit 0
    }
    if (-not $trusted -and -not (Is-Administrator)) {
        $arguments = @('-NoLogo','-NoProfile','-ExecutionPolicy','Bypass','-File',"`"$PSCommandPath`"",'-ImportCertificateOnly') -join ' '
        $elevated = Start-Process -FilePath 'powershell.exe' -ArgumentList $arguments -Verb RunAs -Wait -PassThru
        if ($elevated.ExitCode -ne 0) { throw 'Certificate trust setup was cancelled or failed.' }
        Log 'Certificate trust setup completed through a UAC-elevated helper.'
    } elseif (-not $trusted) { Import-Certificate -FilePath $certPath -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople' | Out-Null; Log 'Imported the public certificate into LocalMachine TrustedPeople.' }
    else { Log 'Matching public certificate is already trusted; no duplicate import performed.' }
    $tempRoot = Join-Path ([IO.Path]::GetTempPath()) ("UrbanPlanToolbox-" + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null
    $localBundlePath = Join-Path $tempRoot $metadata.remoteBundleFileName
    if ($BundlePathOverride) { if (-not (Test-Path -LiteralPath $BundlePathOverride -PathType Leaf)) { throw "Test bundle override does not exist: $BundlePathOverride" }; Copy-Item -LiteralPath $BundlePathOverride -Destination $localBundlePath -Force; Log "Using local bundle override: $localBundlePath." }
    else {
        $release = Invoke-ReleaseMetadataWithRetry $metadata.releaseApiUri @{ Accept='application/vnd.github+json'; 'User-Agent'="UrbanPlanToolbox/$($metadata.displayVersion)" } ${function:Log}
        if ($release.tag_name -ne $metadata.releaseTag -or $release.draft -or $release.prerelease) { throw "Release not found or not stable: $($metadata.releaseTag)" }
        $bundleAssets = @($release.assets | Where-Object { $_.name -like '*.msixbundle' }); $bundleAsset = @($bundleAssets | Where-Object name -eq $metadata.remoteBundleFileName); $checksumAsset = @($release.assets | Where-Object name -eq $metadata.checksumFileName)
        if ($bundleAssets.Count -ne 1 -or $bundleAsset.Count -ne 1) { throw 'Release assets are incomplete.' }
        $bundleDigest = Get-ValidSha256Digest $bundleAsset[0].digest
        $checksumPath = Join-Path $tempRoot $metadata.checksumFileName
        $manifestHash = $null
        Log "ReleaseTag=$($metadata.releaseTag); ExpectedBundleName=$($metadata.remoteBundleFileName); ChecksumAssetFound=$($checksumAsset.Count -eq 1); BundleDigest=$($bundleAsset[0].digest)"
        if ($checksumAsset.Count -eq 1) {
            Download-SmallReleaseAssetWithRetry -Uri $checksumAsset[0].browser_download_url -Destination $checksumPath -ReleaseTag $metadata.releaseTag -AssetName $metadata.checksumFileName -Log ${function:Log}
            $manifestHash = Resolve-Sha256ManifestHash $checksumPath $metadata.remoteBundleFileName
            Log "ChecksumManifestDownloaded=YES; ChecksumManifestMatched=$([bool]$manifestHash)"
        }
        if ($bundleDigest -and $manifestHash -and $bundleDigest -cne $manifestHash) { throw 'Release checksum sources disagree.' }
        $expected = if ($bundleDigest) { $bundleDigest } else { $manifestHash }
        if ([string]::IsNullOrWhiteSpace($expected)) { throw "No valid SHA-256 was available for $($metadata.remoteBundleFileName)." }
        Log "ChecksumSource=$(if ($bundleDigest) { 'GitHubAssetDigest' } else { 'SHA256SUMS' }); ExpectedSHA256=$expected"
        Download-ReleaseAssetRobust -Uri $bundleAsset[0].browser_download_url -Destination $localBundlePath -ExpectedBytes ([long]$bundleAsset[0].size) -ReleaseTag $metadata.releaseTag -AssetName $metadata.remoteBundleFileName -Log ${function:Log}
        $actual = (Get-FileHash -LiteralPath $localBundlePath -Algorithm SHA256).Hash.ToUpperInvariant(); Log "Downloaded $($metadata.releaseTag), Bytes=$((Get-Item $localBundlePath).Length), SHA256=$actual."
        if ($actual -ne $expected) { throw 'Checksum mismatch.' }
    }
    $signature = Get-AuthenticodeSignature -FilePath $localBundlePath
    if (-not $signature.SignerCertificate -or $signature.SignerCertificate.Subject -cne $metadata.publisher -or $signature.SignerCertificate.Thumbprint.ToUpperInvariant() -ne $thumbprint) { throw 'Signature mismatch.' }
    Add-AppxPackage -Path $localBundlePath -ErrorAction Stop
    $installed = @(Get-AppxPackage -Name $metadata.packageIdentityName -ErrorAction SilentlyContinue | Where-Object Publisher -eq $metadata.publisher)
    if ($installed.Count -ne 1) { throw "Package verification failed: expected one package, found $($installed.Count)." }
    $package = $installed[0]
    if ([string]$package.Version -ne $metadata.packageVersion -or [string]$package.Architecture -ne 'X64' -or [string]$package.Status -ne 'Ok') { throw "Package verification failed: Version=$($package.Version); Architecture=$($package.Architecture); Status=$($package.Status)" }
    Log "Verified installed package: Name=$($package.Name); Publisher=$($package.Publisher); Version=$($package.Version); Architecture=$($package.Architecture); Status=$($package.Status)."
    Write-Output "UrbanPlanToolbox v$($metadata.displayVersion) installation completed."
    exit 0
} catch {
    $userMessage = 'Failed to download the installation package. Check your network connection and try again.'
    $culture = [System.Globalization.CultureInfo]::CurrentUICulture.Name
    if ($culture -like 'zh*') { $userMessage = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('5LiL6L295a6J6KOF5YyF5aSx6LSl77yM6K+35qOA5p+l572R57uc6L+e5o6l5ZCO6YeN6K+V44CC')) }
    elseif ($culture -like 'ja*') { $userMessage = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('44Kk44Oz44K544OI44O844OrIOODkeODg+OCseODvOOCuOOBruODgOOCpuODs+ODreODvOODieOBq+WkseaVl+OBl+OBvuOBl+OBn+OAguODjeODg+ODiOODr+ODvOOCr+aOpee2muOCkueiuuiqjeOBl+OBpuOAgeOCguOBhuS4gOW6puOBiuippuOBl+OBj+OBoOOBleOBhOOAgg==')) }
    <#
    $userMessage = switch -Regex ([System.Globalization.CultureInfo]::CurrentUICulture.Name) {
        '^zh' { '下载安装包失败，请检查网络连接后重试。'; break }
        '^ja' { 'インストール パッケージのダウンロードに失敗しました。ネットワーク接続を確認して、もう一度お試しください。'; break }
        default { 'Failed to download the installation package. Check your network connection and try again.' }
    }
    #>
    if ($_.Exception.Message -notin @('DownloadFailed','ChecksumDownloadFailed','ReleaseMetadataFailed')) { $userMessage = $_.Exception.Message }
    Log "Installation bootstrap failed: $userMessage; ExceptionType=$($_.Exception.GetType().FullName); HRESULT=$($_.Exception.HResult); Message=$($_.Exception.Message)"
    Write-Error $userMessage
    exit 1
} finally { Write-Output "INSTALL_LOG_PATH=$logPath" }
