[CmdletBinding()]
param([Parameter(Mandatory)][string]$ReleaseDirectory)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $ReleaseDirectory).Path
$payload = Join-Path $root 'payload'
$metadata = Get-Content -Raw -LiteralPath (Join-Path $payload 'InstallerMetadata.json') -Encoding UTF8 | ConvertFrom-Json
if ([int]$metadata.schemaVersion -ne 2 -or $metadata.displayVersion -ne '1.5.6' -or $metadata.packageVersion -ne '1.5.6.0') { throw 'One-click metadata version mismatch.' }
foreach ($file in @($metadata.bundleFileName,$metadata.certificateFileName,$metadata.appInstallerFileName,'Install.ps1','Uninstall.ps1','InstallLauncher.ps1','UninstallLauncher.ps1','InstallerMetadata.ps1','SHA256SUMS.txt')) { if (-not (Test-Path -LiteralPath (Join-Path $payload $file) -PathType Leaf)) { throw "Missing one-click payload file: $file" } }
$install = Get-Content -Raw -LiteralPath (Join-Path $payload 'Install.ps1')
if ($install -match '(?i)\bAdd-AppxPackage\b') { throw 'One-click installer must not install the application with Add-AppxPackage.' }
if ($install -notmatch 'ms-appinstaller:' -or $metadata.appInstallerUri -ne 'https://kiyoujyo.github.io/UrbanPlanToolbox/UrbanPlanToolbox.appinstaller') { throw 'One-click installer does not invoke the stable App Installer URI.' }
[xml]$appInstaller = Get-Content -Raw -LiteralPath (Join-Path $payload $metadata.appInstallerFileName)
$bundle = $appInstaller.SelectSingleNode("/*[local-name()='AppInstaller']/*[local-name()='MainBundle']")
if ($null -eq $bundle -or $bundle.Version -ne $metadata.packageVersion -or $bundle.Publisher -ne $metadata.publisher) { throw 'Embedded App Installer metadata mismatch.' }
$hashes = @{}
Get-Content -LiteralPath (Join-Path $payload 'SHA256SUMS.txt') | ForEach-Object { if ($_ -match '^(?<hash>[A-Fa-f0-9]{64}) \*(?<name>.+)$') { $hashes[$matches.name] = $matches.hash.ToUpperInvariant() } }
Get-ChildItem -LiteralPath $payload -Recurse -File | Where-Object Name -ne 'SHA256SUMS.txt' | ForEach-Object { $relative = $_.FullName.Substring($payload.Length).TrimStart('\').Replace('\','/'); if (-not $hashes.ContainsKey($relative)) { throw "SHA256SUMS.txt missing $relative" }; if ((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToUpperInvariant() -ne $hashes[$relative]) { throw "SHA-256 mismatch: $relative" } }
Write-Output 'GitHub one-click installer package validation passed.'
