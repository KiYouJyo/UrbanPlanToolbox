[CmdletBinding()]
param([switch]$LaunchAfterInstall, [switch]$ImportCertificateOnly, [string]$BundlePathOverride)
$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8; $OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }
$payloadRoot = $PSScriptRoot
. (Join-Path $payloadRoot 'InstallerMetadata.ps1')
$logDirectory = Join-Path $env:LOCALAPPDATA 'UrbanPlanToolbox\Logs'
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
$logPath = Join-Path $logDirectory ("Install-{0:yyyyMMdd-HHmmss}.log" -f (Get-Date))
function Log([string]$Message) { "{0:u} {1}" -f (Get-Date), $Message | Tee-Object -FilePath $logPath -Append }
function Is-Administrator { $p = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent()); $p.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator) }
try {
    Write-Output '正在验证安装包...'
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
    if ($BundlePathOverride) {
        if (-not (Test-Path -LiteralPath $BundlePathOverride -PathType Leaf)) { throw "Test bundle override does not exist: $BundlePathOverride" }
        Copy-Item -LiteralPath $BundlePathOverride -Destination $localBundlePath -Force
        Log "Using local bundle override for controlled E2E test: $localBundlePath."
    } else {
        try {
            $release = Invoke-RestMethod -Uri $metadata.releaseApiUri -Headers @{ Accept = 'application/vnd.github+json'; 'User-Agent' = "UrbanPlanToolbox/$($metadata.displayVersion)" } -Method Get
        } catch { Log "GitHub release lookup failed: URI=$($metadata.releaseApiUri); Error=$($_.Exception.Message)"; throw 'Unable to contact GitHub.' }
        if ($release.tag_name -ne $metadata.releaseTag -or $release.draft -or $release.prerelease) { throw "Release not found or not stable: $($metadata.releaseTag)" }
        $bundleAssets = @($release.assets | Where-Object { $_.name -like '*.msixbundle' })
        $bundleAsset = @($bundleAssets | Where-Object name -eq $metadata.remoteBundleFileName)
        $checksumAsset = @($release.assets | Where-Object name -eq $metadata.checksumFileName)
        if ($bundleAssets.Count -ne 1) { throw "Expected exactly one MSIXBundle asset; found $($bundleAssets.Count)." }
        if ($bundleAsset.Count -ne 1) { throw "Bundle asset not found: $($metadata.remoteBundleFileName)" }
        if ($checksumAsset.Count -ne 1) { throw "Checksum asset not found: $($metadata.checksumFileName)" }
        foreach ($asset in @($bundleAsset[0], $checksumAsset[0])) { $uri = [Uri]$asset.browser_download_url; if ($uri.Scheme -ne 'https' -or $uri.Host -ne 'github.com') { throw 'Release asset URL is not an allowed GitHub HTTPS URL.' } }
        $checksumPath = Join-Path $tempRoot $metadata.checksumFileName
        try { Invoke-WebRequest -Uri $checksumAsset[0].browser_download_url -UseBasicParsing -OutFile $checksumPath -MaximumRedirection 10 -ErrorAction Stop } catch { Log "Checksum download failed: Error=$($_.Exception.Message)"; throw 'Checksum download failed.' }
        $expected = (Get-Content -LiteralPath $checksumPath | Where-Object { $_ -match "^(?<hash>[A-Fa-f0-9]{64})\s+\*?$([regex]::Escape($metadata.remoteBundleFileName))$" } | ForEach-Object { $matches.hash.ToUpperInvariant() } | Select-Object -First 1)
        if ([string]::IsNullOrWhiteSpace($expected)) { throw "Checksum missing for $($metadata.remoteBundleFileName)." }
        try { Invoke-WebRequest -Uri $bundleAsset[0].browser_download_url -UseBasicParsing -OutFile $localBundlePath -MaximumRedirection 10 -ErrorAction Stop } catch { Log "Bundle download failed: URL host=$(([Uri]$bundleAsset[0].browser_download_url).Host); Error=$($_.Exception.Message)"; throw 'Bundle download failed.' }
        if (-not (Test-Path -LiteralPath $localBundlePath -PathType Leaf) -or (Get-Item $localBundlePath).Length -le 0) { throw 'Downloaded bundle is missing or empty.' }
        $actual = (Get-FileHash -LiteralPath $localBundlePath -Algorithm SHA256).Hash.ToUpperInvariant()
        Log "Downloaded Release=$($metadata.releaseTag), Asset=$($metadata.remoteBundleFileName), Bytes=$((Get-Item $localBundlePath).Length), SHA256=$actual."
        if ($actual -ne $expected) { Log "Checksum mismatch: Expected=$expected; Actual=$actual"; throw 'Checksum mismatch.' }
    }
    $signature = Get-AuthenticodeSignature -FilePath $localBundlePath
    if (-not $signature.SignerCertificate -or $signature.SignerCertificate.Subject -cne $metadata.publisher -or $signature.SignerCertificate.Thumbprint.ToUpperInvariant() -ne $thumbprint) { throw 'Signature mismatch.' }
    Log "Signature verified: Status=$($signature.Status); Publisher=$($signature.SignerCertificate.Subject)."
    Write-Output '正在安装 UrbanPlanToolbox...'
    try { Add-AppxPackage -Path $localBundlePath -ErrorAction Stop } catch { Log "Package deployment failed: $($_.Exception.Message)"; throw "Package deployment failed: $($_.Exception.Message)" }
    $installed = @(Get-AppxPackage -Name $metadata.packageIdentityName -ErrorAction SilentlyContinue | Where-Object Publisher -eq $metadata.publisher)
    if ($installed.Count -ne 1) { throw "Package verification failed: expected one installed package, found $($installed.Count)." }
    $package = $installed[0]
    if ([string]$package.Version -ne $metadata.packageVersion -or [string]$package.Architecture -ne 'X64' -or [string]$package.Status -ne 'Ok') { throw "Package verification failed: Name=$($package.Name); Publisher=$($package.Publisher); Version=$($package.Version); Architecture=$($package.Architecture); Status=$($package.Status)" }
    Log "Verified installed package: Name=$($package.Name); Publisher=$($package.Publisher); Version=$($package.Version); Architecture=$($package.Architecture); Status=$($package.Status)."
    Write-Output "UrbanPlanToolbox v$($metadata.displayVersion) 安装完成。"
    exit 0
} catch { Log "Installation bootstrap failed: $($_.Exception.Message)"; Write-Error $_; exit 1 } finally { Write-Output "INSTALL_LOG_PATH=$logPath" }
