[CmdletBinding()]
param([Parameter(Mandatory)][string]$ReleaseDirectory, [string]$ZipPath)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $ReleaseDirectory).Path
$payload = Join-Path $root 'payload'
$rootCmdFiles = @(Get-ChildItem -LiteralPath $root -File -Filter '*.cmd' | Sort-Object Name)
if ($rootCmdFiles.Count -ne 2 -or $rootCmdFiles[0].Name[0] -ne [char]0x2460 -or $rootCmdFiles[1].Name[0] -ne [char]0x2461) { throw 'Missing one-click root installer/uninstaller CMD entry files.' }
$rootTextFiles = @(Get-ChildItem -LiteralPath $root -File -Filter '*.txt')
if ($rootTextFiles.Count -ne 1 -or $rootTextFiles[0].Name[0] -ne [char]0x8BF7) { throw 'Missing one-click root readme text file.' }
if (-not (Test-Path -LiteralPath $payload -PathType Container)) { throw 'Missing one-click payload directory: payload' }
$metadata = Get-Content -Raw -LiteralPath (Join-Path $payload 'InstallerMetadata.json') -Encoding UTF8 | ConvertFrom-Json
if ([int]$metadata.schemaVersion -ne 3 -or $metadata.displayVersion -ne '1.5.6' -or $metadata.packageVersion -ne '1.5.6.0') { throw 'One-click metadata version mismatch.' }
foreach ($file in @($metadata.certificateFileName,'Install.ps1','Uninstall.ps1','InstallLauncher.ps1','UninstallLauncher.ps1','InstallerMetadata.ps1','SHA256SUMS.txt')) { if (-not (Test-Path -LiteralPath (Join-Path $payload $file) -PathType Leaf)) { throw "Missing one-click payload file: $file" } }
$embeddedPackages = @(Get-ChildItem -LiteralPath $root -Recurse -File | Where-Object { $_.Extension.ToLowerInvariant() -in @('.msix','.msixbundle','.appinstaller','.pfx','.p12') })
if ($embeddedPackages.Count -gt 0) { throw "One-click bootstrap must not embed application packages or App Installer files: $($embeddedPackages.Name -join ', ')" }
$install = Get-Content -Raw -LiteralPath (Join-Path $payload 'Install.ps1')
$installLauncher = Get-Content -Raw -LiteralPath (Join-Path $payload 'InstallLauncher.ps1')
$installCommand = Get-Content -Raw -LiteralPath $rootCmdFiles[0].FullName
$uninstallCommand = Get-Content -Raw -LiteralPath $rootCmdFiles[1].FullName
foreach ($entry in @($installCommand, $uninstallCommand)) { if ($entry.ToCharArray() | Where-Object { [int]$_ -gt 127 }) { throw 'Root CMD entry files must contain ASCII-only content.' } }
$forbiddenProtocolFiles = @{
    'Install.ps1' = $install
    'InstallLauncher.ps1' = $installLauncher
    $rootCmdFiles[0].Name = $installCommand
}
foreach ($entry in $forbiddenProtocolFiles.GetEnumerator()) {
    if ($entry.Value -match '(?i)ms-appinstaller:') { throw "One-click installer must not rely on the disabled ms-appinstaller URI protocol: $($entry.Key)" }
}
if ($install -match '(?i)Add-AppxPackage\s+-AppInstallerFile|RequestAddPackageByAppInstallerFileAsync|GetAppInstallerInfo|ms-appinstaller:') { throw 'One-click installer must not use App Installer deployment or association.' }
if ($install -notmatch '(?i)Invoke-RestMethod\s+-Uri\s+\$metadata\.releaseApiUri' -or $install -notmatch '(?i)Invoke-WebRequest\s+-Uri\s+\$bundleAsset') { throw 'One-click installer does not download the GitHub Release assets.' }
if ($install -notmatch '(?i)Get-FileHash\s+-LiteralPath\s+\$localBundlePath\s+-Algorithm\s+SHA256') { throw 'One-click installer does not verify the downloaded bundle checksum.' }
if ($install -notmatch '(?i)Get-AuthenticodeSignature') { throw 'One-click installer does not verify the bundle signature.' }
if ($install -notmatch '(?i)Add-AppxPackage\s+-Path\s+\$localBundlePath') { throw 'One-click installer must deploy the verified local MSIXBundle.' }
if ($metadata.releaseApiUri -ne 'https://api.github.com/repos/KiYouJyo/UrbanPlanToolbox/releases/latest' -or $metadata.releaseTag -ne 'v1.5.6' -or $metadata.checksumFileName -ne 'SHA256SUMS.txt') { throw 'One-click metadata does not describe the GitHub Release asset flow.' }
if ($install -match '(?i)explorer\.exe|Start-Process\s+-FilePath\s+\$localAppInstallerPath') { throw 'One-click installer must not use GUI/file-association launching as the normal installation path.' }
if ($install -match '(?i)Installation completed successfully') { throw 'Bootstrap must not claim application installation before deployment verification.' }
foreach ($required in @('Get-AppxPackage','packageIdentityName','publisher','packageVersion','Architecture','Status')) { if ($install -notmatch [regex]::Escape($required)) { throw "One-click installer is missing package verification: $required" } }
if ($installCommand -notmatch '(?i)payload\\InstallLauncher\.ps1') { throw 'Install root command does not reference payload\\InstallLauncher.ps1.' }
if ($uninstallCommand -notmatch '(?i)payload\\UninstallLauncher\.ps1') { throw 'Uninstall root command does not reference payload\\UninstallLauncher.ps1.' }
if ($installCommand -match '(?i)(Install\.ps1|Add-AppxPackage|\.msixbundle)') { throw 'Install root command references a legacy payload or direct package installation.' }
if ($uninstallCommand -match '(?i)(Uninstall\.ps1|\.msixbundle)') { throw 'Uninstall root command references a legacy payload or package filename.' }
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
