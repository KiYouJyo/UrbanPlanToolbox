[CmdletBinding()]
param([Parameter(Mandatory)][string]$ReleaseDirectory, [string]$ZipPath)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $ReleaseDirectory).Path
$payload = Join-Path $root 'payload'

$expectedRootFileNames = @('1-Install-UrbanPlanToolbox.cmd','2-Uninstall-UrbanPlanToolbox.cmd','README.txt') | Sort-Object
$actualRootFileNames = @(Get-ChildItem -LiteralPath $root -File | Select-Object -ExpandProperty Name | Sort-Object)
if (($actualRootFileNames -join "`n") -cne ($expectedRootFileNames -join "`n")) { throw "Unexpected one-click root files. Expected: $($expectedRootFileNames -join ', '); actual: $($actualRootFileNames -join ', ')" }
$nonAsciiRootNames = @($actualRootFileNames | Where-Object { $_.ToCharArray() | Where-Object { [int]$_ -gt 127 } })
if ($nonAsciiRootNames.Count -gt 0) { throw "One-click root filenames must remain ASCII/English: $($nonAsciiRootNames -join ', ')" }

$installCommandPath = Join-Path $root '1-Install-UrbanPlanToolbox.cmd'
$uninstallCommandPath = Join-Path $root '2-Uninstall-UrbanPlanToolbox.cmd'
$readmePath = Join-Path $root 'README.txt'
if (-not (Test-Path -LiteralPath $payload -PathType Container)) { throw 'Missing one-click payload directory: payload' }
$readme = Get-Content -Raw -LiteralPath $readmePath -Encoding UTF8
foreach ($requiredReadmeText in @('English','1-Install-UrbanPlanToolbox.cmd','2-Uninstall-UrbanPlanToolbox.cmd','中文','日本語')) {
    if ($readme -notmatch [regex]::Escape($requiredReadmeText)) { throw "README.txt is missing international installer guidance: $requiredReadmeText" }
}

$metadata = Get-Content -Raw -LiteralPath (Join-Path $payload 'InstallerMetadata.json') -Encoding UTF8 | ConvertFrom-Json
$displayVersionText = [string]$metadata.displayVersion
$packageVersionText = [string]$metadata.packageVersion
$packageDisplayVersion = if ($packageVersionText -match '^(?<display>\d+\.\d+\.\d+)\.\d+$') { $matches.display } else { '' }
if ([int]$metadata.schemaVersion -ne 3 -or $displayVersionText -notmatch '^\d+\.\d+\.\d+$' -or $packageDisplayVersion -ne $displayVersionText) { throw 'One-click metadata version mismatch.' }
foreach ($file in @($metadata.certificateFileName,'Install.ps1','Uninstall.ps1','InstallLauncher.ps1','UninstallLauncher.ps1','InstallerMetadata.ps1','ChecksumResolver.ps1','ReleaseDownloadResolver.ps1','SHA256SUMS.txt')) { if (-not (Test-Path -LiteralPath (Join-Path $payload $file) -PathType Leaf)) { throw "Missing one-click payload file: $file" } }
$embeddedPackages = @(Get-ChildItem -LiteralPath $root -Recurse -File | Where-Object { $_.Extension.ToLowerInvariant() -in @('.msix','.msixbundle','.appinstaller','.pfx','.p12') })
if ($embeddedPackages.Count -gt 0) { throw "One-click bootstrap must not embed application packages or App Installer files: $($embeddedPackages.Name -join ', ')" }
$install = Get-Content -Raw -LiteralPath (Join-Path $payload 'Install.ps1')
$downloadResolver = Get-Content -Raw -LiteralPath (Join-Path $payload 'ReleaseDownloadResolver.ps1')
$installLauncher = Get-Content -Raw -LiteralPath (Join-Path $payload 'InstallLauncher.ps1')
$uninstall = Get-Content -Raw -LiteralPath (Join-Path $payload 'Uninstall.ps1')
$uninstallLauncher = Get-Content -Raw -LiteralPath (Join-Path $payload 'UninstallLauncher.ps1')
$installCommand = Get-Content -Raw -LiteralPath $installCommandPath
$uninstallCommand = Get-Content -Raw -LiteralPath $uninstallCommandPath
foreach ($entry in @($installCommand, $uninstallCommand)) { if ($entry.ToCharArray() | Where-Object { [int]$_ -gt 127 }) { throw 'Root CMD entry files must contain ASCII-only content.' } }
$forbiddenProtocolFiles = @{
    'Install.ps1' = $install
    'InstallLauncher.ps1' = $installLauncher
    '1-Install-UrbanPlanToolbox.cmd' = $installCommand
}
foreach ($entry in $forbiddenProtocolFiles.GetEnumerator()) {
    if ($entry.Value -match '(?i)ms-appinstaller:') { throw "One-click installer must not rely on the disabled ms-appinstaller URI protocol: $($entry.Key)" }
}
if ($install -match '(?i)Add-AppxPackage\s+-AppInstallerFile|RequestAddPackageByAppInstallerFileAsync|GetAppInstallerInfo|ms-appinstaller:') { throw 'One-click installer must not use App Installer deployment or association.' }
if ($install -notmatch '(?i)Invoke-ReleaseMetadataWithRetry\s+\$metadata\.releaseApiUri' -or $install -notmatch '(?i)Download-ReleaseAssetRobust') { throw 'One-click installer does not use the resilient GitHub Release bundle downloader.' }
if ($install -notmatch '(?i)Download-SmallReleaseAssetWithRetry') { throw 'One-click installer does not retry the checksum asset download.' }
if ($downloadResolver -notmatch '(?i)Start-BitsTransfer' -or $downloadResolver -notmatch '(?i)RetryInterval' -or $downloadResolver -notmatch '(?i)RetryTimeout') { throw 'One-click installer is missing BITS retry support.' }
if ($downloadResolver -notmatch '(?i)ExpectedBytes|IncompleteDownload|ActualBytes') { throw 'One-click installer does not validate the downloaded bundle size.' }
if ($downloadResolver -notmatch '(?i)DownloadMethod=BITS|DownloadMethod=InvokeWebRequest|BundleDownloadCompleted|BundleDownloadFailed') { throw 'One-click installer is missing download diagnostics.' }
if ($install -notmatch '(?i)Get-FileHash\s+-LiteralPath\s+\$localBundlePath\s+-Algorithm\s+SHA256') { throw 'One-click installer does not verify the downloaded bundle checksum.' }
if ($install -notmatch '(?i)Get-AuthenticodeSignature') { throw 'One-click installer does not verify the bundle signature.' }
if ($install -notmatch '(?i)Add-AppxPackage\s+-Path\s+\$localBundlePath') { throw 'One-click installer must deploy the verified local MSIXBundle.' }
if ($metadata.releaseApiUri -ne "https://api.github.com/repos/KiYouJyo/UrbanPlanToolbox/releases/tags/v$($metadata.displayVersion)" -or $metadata.releaseTag -ne "v$($metadata.displayVersion)" -or $metadata.checksumFileName -ne 'SHA256SUMS.txt') { throw 'One-click metadata does not describe the fixed GitHub Release asset flow.' }
if ($install -match '(?i)explorer\.exe|Start-Process\s+-FilePath\s+\$localAppInstallerPath') { throw 'One-click installer must not use GUI/file-association launching as the normal installation path.' }
if ($install -match '(?i)Installation completed successfully') { throw 'Bootstrap must not claim application installation before deployment verification.' }
foreach ($required in @('Get-AppxPackage','packageIdentityName','publisher','packageVersion','Architecture','Status')) { if ($install -notmatch [regex]::Escape($required)) { throw "One-click installer is missing package verification: $required" } }
if ($installCommand -notmatch '(?i)payload\\InstallLauncher\.ps1') { throw 'Install root command does not reference payload\\InstallLauncher.ps1.' }
if ($uninstallCommand -notmatch '(?i)payload\\UninstallLauncher\.ps1') { throw 'Uninstall root command does not reference payload\\UninstallLauncher.ps1.' }
if ($installCommand -match '(?i)(Install\.ps1|Add-AppxPackage|\.msixbundle)') { throw 'Install root command references a legacy payload or direct package installation.' }
if ($uninstallCommand -match '(?i)(Uninstall\.ps1|\.msixbundle)') { throw 'Uninstall root command references a legacy payload or package filename.' }
if ($uninstallLauncher -match '(?i)-RemoveTestCertificate') { throw 'Uninstall launcher passes an unsupported payload parameter.' }
if ($uninstallLauncher -match '(?i)-RemoveCertificate') {
    if ($uninstall -notmatch '(?ms)param\s*\(.*?\[switch\]\s*\$RemoveCertificate') { throw 'Uninstall launcher passes an unsupported payload parameter.' }
}
if ($uninstall -match '(?i)Get-AppxPackage\s+-AllUsers|Get-MsixPackageMetadata|msixFileName|RemoveTestCertificate') { throw 'Uninstall payload must not depend on a local MSIX or enumerate all users.' }
foreach ($required in @('556F80C5-C4D4-452B-93B4-00DE3FA7AC29','CN=AppPublisher','Get-AppxPackage','Remove-AppxPackage','TrustedPeople')) { if ($uninstall -notmatch [regex]::Escape($required)) { throw "Uninstall payload is missing exact identity or removal operation: $required" } }
if ($installCommand -match '(?i)Installation completed successfully') { throw 'Install root command must not claim application installation before package confirmation.' }
if ([string]::IsNullOrWhiteSpace($metadata.remoteBundleFileName)) { throw 'One-click metadata is missing remoteBundleFileName.' }
$hashes = @{}
Get-Content -LiteralPath (Join-Path $payload 'SHA256SUMS.txt') | ForEach-Object { if ($_ -match '^(?<hash>[A-Fa-f0-9]{64}) \*(?<name>.+)$') { $hashes[$matches.name] = $matches.hash.ToUpperInvariant() } }
Get-ChildItem -LiteralPath $payload -Recurse -File | Where-Object Name -ne 'SHA256SUMS.txt' | ForEach-Object { $relative = $_.FullName.Substring($payload.Length).TrimStart('\').Replace('\','/'); if (-not $hashes.ContainsKey($relative)) { throw "SHA256SUMS.txt missing $relative" }; if ((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToUpperInvariant() -ne $hashes[$relative]) { throw "SHA-256 mismatch: $relative" } }
if ($ZipPath) {
    $zip = Get-Item -LiteralPath $ZipPath -ErrorAction Stop
    if ($zip.Length -gt 5MB) { throw 'One-click bootstrap is unexpectedly large. Check for embedded application packages or build artifacts.' }
}
Write-Output 'GitHub one-click installer package validation passed.'
