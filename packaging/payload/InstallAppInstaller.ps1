[CmdletBinding()]
param([switch]$LaunchAfterInstall, [switch]$ImportCertificateOnly, [string]$BundlePathOverride)
$ErrorActionPreference = 'Stop'
$payloadRoot = $PSScriptRoot
. (Join-Path $payloadRoot 'InstallerMetadata.ps1')
. (Join-Path $payloadRoot 'ChecksumResolver.ps1')
$logDirectory = Join-Path $env:LOCALAPPDATA 'UrbanPlanToolbox\Logs'
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
$logPath = Join-Path $logDirectory ("Install-{0:yyyyMMdd-HHmmss}.log" -f (Get-Date))
function Log([string]$Message) { "{0:u} {1}" -f (Get-Date), $Message | Tee-Object -FilePath $logPath -Append }
function Is-Administrator { $p = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent()); $p.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator) }
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
        $release = Invoke-RestMethod -Uri $metadata.releaseApiUri -Headers @{ Accept='application/vnd.github+json'; 'User-Agent'="UrbanPlanToolbox/$($metadata.displayVersion)" } -Method Get
        if ($release.tag_name -ne $metadata.releaseTag -or $release.draft -or $release.prerelease) { throw "Release not found or not stable: $($metadata.releaseTag)" }
        $bundleAssets = @($release.assets | Where-Object { $_.name -like '*.msixbundle' }); $bundleAsset = @($bundleAssets | Where-Object name -eq $metadata.remoteBundleFileName); $checksumAsset = @($release.assets | Where-Object name -eq $metadata.checksumFileName)
        if ($bundleAssets.Count -ne 1 -or $bundleAsset.Count -ne 1) { throw 'Release assets are incomplete.' }
        $bundleDigest = Get-ValidSha256Digest $bundleAsset[0].digest
        $checksumPath = Join-Path $tempRoot $metadata.checksumFileName
        $manifestHash = $null
        Log "ReleaseTag=$($metadata.releaseTag); ExpectedBundleName=$($metadata.remoteBundleFileName); ChecksumAssetFound=$($checksumAsset.Count -eq 1); BundleDigest=$($bundleAsset[0].digest)"
        if ($checksumAsset.Count -eq 1) {
            Invoke-WebRequest -Uri $checksumAsset[0].browser_download_url -UseBasicParsing -OutFile $checksumPath -ErrorAction Stop
            $manifestHash = Resolve-Sha256ManifestHash $checksumPath $metadata.remoteBundleFileName
            Log "ChecksumManifestDownloaded=YES; ChecksumManifestMatched=$([bool]$manifestHash)"
        }
        if ($bundleDigest -and $manifestHash -and $bundleDigest -cne $manifestHash) { throw 'Release checksum sources disagree.' }
        $expected = if ($bundleDigest) { $bundleDigest } else { $manifestHash }
        if ([string]::IsNullOrWhiteSpace($expected)) { throw "No valid SHA-256 was available for $($metadata.remoteBundleFileName)." }
        Log "ChecksumSource=$(if ($bundleDigest) { 'GitHubAssetDigest' } else { 'SHA256SUMS' }); ExpectedSHA256=$expected"
        Invoke-WebRequest -Uri $bundleAsset[0].browser_download_url -UseBasicParsing -OutFile $localBundlePath -ErrorAction Stop
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
} catch { Log "Installation bootstrap failed: $($_.Exception.Message)"; Write-Error $_; exit 1 } finally { Write-Output "INSTALL_LOG_PATH=$logPath" }
