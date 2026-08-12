[CmdletBinding()]
param([Parameter(Mandatory)][string]$ReleaseDirectory)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $ReleaseDirectory).Path
$payload = Join-Path $root 'payload'
$rootCmdFiles = @(Get-ChildItem -LiteralPath $root -File -Filter '*.cmd' | Sort-Object Name)
if ($rootCmdFiles.Count -ne 2 -or $rootCmdFiles[0].Name[0] -ne [char]0x2460 -or $rootCmdFiles[1].Name[0] -ne [char]0x2461) { throw 'Missing one-click root installer/uninstaller CMD entry files.' }
$rootTextFiles = @(Get-ChildItem -LiteralPath $root -File -Filter '*.txt')
if ($rootTextFiles.Count -ne 1 -or $rootTextFiles[0].Name[0] -ne [char]0x8BF7) { throw 'Missing one-click root readme text file.' }
if (-not (Test-Path -LiteralPath $payload -PathType Container)) { throw 'Missing one-click payload directory: payload' }
$metadata = Get-Content -Raw -LiteralPath (Join-Path $payload 'InstallerMetadata.json') -Encoding UTF8 | ConvertFrom-Json
if ([int]$metadata.schemaVersion -ne 2 -or $metadata.displayVersion -ne '1.5.6' -or $metadata.packageVersion -ne '1.5.6.0') { throw 'One-click metadata version mismatch.' }
foreach ($file in @($metadata.bundleFileName,$metadata.certificateFileName,$metadata.appInstallerFileName,'Install.ps1','Uninstall.ps1','InstallLauncher.ps1','UninstallLauncher.ps1','InstallerMetadata.ps1','SHA256SUMS.txt')) { if (-not (Test-Path -LiteralPath (Join-Path $payload $file) -PathType Leaf)) { throw "Missing one-click payload file: $file" } }
$install = Get-Content -Raw -LiteralPath (Join-Path $payload 'Install.ps1')
if ($install -match '(?i)\bAdd-AppxPackage\b') { throw 'One-click installer must not install the application with Add-AppxPackage.' }
if ($install -notmatch 'ms-appinstaller:' -or $metadata.appInstallerUri -ne 'https://kiyoujyo.github.io/UrbanPlanToolbox/UrbanPlanToolbox.appinstaller') { throw 'One-click installer does not invoke the stable App Installer URI.' }
$installCommand = Get-Content -Raw -LiteralPath $rootCmdFiles[0].FullName
$uninstallCommand = Get-Content -Raw -LiteralPath $rootCmdFiles[1].FullName
if ($installCommand -notmatch '(?i)payload\\InstallLauncher\.ps1') { throw 'Install root command does not reference payload\\InstallLauncher.ps1.' }
if ($uninstallCommand -notmatch '(?i)payload\\UninstallLauncher\.ps1') { throw 'Uninstall root command does not reference payload\\UninstallLauncher.ps1.' }
if ($installCommand -match '(?i)(Install\.ps1|Add-AppxPackage|\.msixbundle)') { throw 'Install root command references a legacy payload or direct package installation.' }
if ($uninstallCommand -match '(?i)(Uninstall\.ps1|\.msixbundle)') { throw 'Uninstall root command references a legacy payload or package filename.' }
[xml]$appInstaller = Get-Content -Raw -LiteralPath (Join-Path $payload $metadata.appInstallerFileName)
$bundle = $appInstaller.SelectSingleNode("/*[local-name()='AppInstaller']/*[local-name()='MainBundle']")
if ($null -eq $bundle -or $bundle.Version -ne $metadata.packageVersion -or $bundle.Publisher -ne $metadata.publisher) { throw 'Embedded App Installer metadata mismatch.' }
$hashes = @{}
Get-Content -LiteralPath (Join-Path $payload 'SHA256SUMS.txt') | ForEach-Object { if ($_ -match '^(?<hash>[A-Fa-f0-9]{64}) \*(?<name>.+)$') { $hashes[$matches.name] = $matches.hash.ToUpperInvariant() } }
Get-ChildItem -LiteralPath $payload -Recurse -File | Where-Object Name -ne 'SHA256SUMS.txt' | ForEach-Object { $relative = $_.FullName.Substring($payload.Length).TrimStart('\').Replace('\','/'); if (-not $hashes.ContainsKey($relative)) { throw "SHA256SUMS.txt missing $relative" }; if ((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToUpperInvariant() -ne $hashes[$relative]) { throw "SHA-256 mismatch: $relative" } }
Write-Output 'GitHub one-click installer package validation passed.'
