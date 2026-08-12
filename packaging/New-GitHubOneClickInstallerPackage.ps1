[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SignedBundlePath,
    [Parameter(Mandatory)][string]$PublicCertificatePath,
    [Parameter(Mandatory)][string]$AppInstallerPath,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [Parameter(Mandatory)][string]$DisplayVersion,
    [Parameter(Mandatory)][string]$PackageVersion
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$out = [IO.Path]::GetFullPath($OutputDirectory)
$repoPrefix = $repo.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
if ($out -eq $repo -or $out.StartsWith($repoPrefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Output must be outside the repository.' }
if ($DisplayVersion -notmatch '^\d+\.\d+\.\d+$' -or $PackageVersion -notmatch '^\d+\.\d+\.\d+\.\d+$' -or -not $PackageVersion.StartsWith("$DisplayVersion.")) { throw 'Invalid version input.' }
foreach ($path in @($SignedBundlePath, $PublicCertificatePath, $AppInstallerPath)) { if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing input: $path" } }

$bundle = Get-Item -LiteralPath $SignedBundlePath
$cert = [Security.Cryptography.X509Certificates.X509Certificate2]::new((Resolve-Path $PublicCertificatePath))
if ($cert.HasPrivateKey -or $cert.Subject -cne 'CN=AppPublisher') { throw 'Invalid public certificate.' }
$appInstaller = [xml](Get-Content -Raw -LiteralPath $AppInstallerPath)
$bundleNode = $appInstaller.SelectSingleNode("/*[local-name()='AppInstaller']/*[local-name()='MainBundle']")
if ($null -eq $bundleNode -or $bundleNode.Version -ne $PackageVersion) { throw 'App Installer does not describe the requested package version.' }

$root = Join-Path $out "UrbanPlanToolbox-v$DisplayVersion-x64-one-click"
if (Test-Path -LiteralPath $root) { throw 'Output already exists.' }
$payload = Join-Path $root 'payload'
New-Item -ItemType Directory -Path $payload -Force | Out-Null
$metadata = [ordered]@{
    schemaVersion = 2; displayVersion = $DisplayVersion; packageVersion = $PackageVersion
    packageIdentityName = '556F80C5-C4D4-452B-93B4-00DE3FA7AC29'; publisher = 'CN=AppPublisher'; architecture = 'x64'
    bundleFileName = $bundle.Name; certificateFileName = "UrbanPlanToolbox-v$DisplayVersion-Framework-Dependent.cer"
    appInstallerFileName = 'UrbanPlanToolbox.appinstaller'; appInstallerUri = 'https://kiyoujyo.github.io/UrbanPlanToolbox/UrbanPlanToolbox.appinstaller'
}
$metadata | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $payload 'InstallerMetadata.json') -Encoding UTF8
$rootEntryScripts = @(Get-ChildItem -LiteralPath $PSScriptRoot -File -Filter '*.cmd' | Sort-Object Name)
if ($rootEntryScripts.Count -ne 2) { throw "Expected exactly two root CMD entry scripts, found $($rootEntryScripts.Count)." }
foreach ($entryScript in $rootEntryScripts) { Copy-Item -LiteralPath $entryScript.FullName -Destination $root }
$readme = Get-ChildItem -LiteralPath $PSScriptRoot -File -Filter '*.txt' | Select-Object -First 1
(Get-Content -Raw -LiteralPath $readme.FullName -Encoding UTF8).Replace('{{DISPLAY_VERSION}}',$DisplayVersion).Replace('{{PACKAGE_VERSION}}',$PackageVersion) | Set-Content -LiteralPath (Join-Path $root $readme.Name) -Encoding UTF8
function Copy-PayloadPowerShellScript([string]$SourceName, [string]$DestinationName) {
    $content = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot $SourceName) -Encoding UTF8
    [IO.File]::WriteAllText((Join-Path $payload $DestinationName), $content, [Text.UTF8Encoding]::new($true))
}
Copy-PayloadPowerShellScript 'payload\InstallAppInstaller.ps1' 'Install.ps1'
Copy-PayloadPowerShellScript 'payload\UninstallAppInstaller.ps1' 'Uninstall.ps1'
Copy-PayloadPowerShellScript 'payload\InstallAppInstallerLauncher.ps1' 'InstallLauncher.ps1'
Copy-PayloadPowerShellScript 'payload\UninstallLauncher.ps1' 'UninstallLauncher.ps1'
Copy-PayloadPowerShellScript 'payload\InstallerMetadataAppInstaller.ps1' 'InstallerMetadata.ps1'
Copy-Item -LiteralPath $SignedBundlePath -Destination (Join-Path $payload $bundle.Name)
Copy-Item -LiteralPath $PublicCertificatePath -Destination (Join-Path $payload $metadata.certificateFileName)
Copy-Item -LiteralPath $AppInstallerPath -Destination (Join-Path $payload $metadata.appInstallerFileName)
$hashLines = Get-ChildItem -LiteralPath $payload -Recurse -File | Where-Object Name -ne 'SHA256SUMS.txt' | Sort-Object FullName | ForEach-Object { "$((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToUpperInvariant()) *$($_.FullName.Substring($payload.Length).TrimStart('\'))" }
Set-Content -LiteralPath (Join-Path $payload 'SHA256SUMS.txt') -Value $hashLines -Encoding UTF8
Write-Output $root
