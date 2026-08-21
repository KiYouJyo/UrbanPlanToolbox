[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SignedBundlePath,
    [Parameter(Mandatory)][string]$PublicCertificatePath,
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
foreach ($path in @($SignedBundlePath, $PublicCertificatePath)) { if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing input: $path" } }

$bundle = Get-Item -LiteralPath $SignedBundlePath
$cert = [Security.Cryptography.X509Certificates.X509Certificate2]::new((Resolve-Path $PublicCertificatePath))
if ($cert.HasPrivateKey -or $cert.Subject -cne 'CN=AppPublisher') { throw 'Invalid public certificate.' }

$root = Join-Path $out "UrbanPlanToolbox-v$DisplayVersion-x64-one-click"
if (Test-Path -LiteralPath $root) { throw 'Output already exists.' }
$payload = Join-Path $root 'payload'
New-Item -ItemType Directory -Path $payload -Force | Out-Null
$metadata = [ordered]@{
    schemaVersion = 3; displayVersion = $DisplayVersion; packageVersion = $PackageVersion; releaseTag = "v$DisplayVersion"
    packageIdentityName = '556F80C5-C4D4-452B-93B4-00DE3FA7AC29'; publisher = 'CN=AppPublisher'; architecture = 'x64'
    remoteBundleFileName = $bundle.Name; certificateFileName = "UrbanPlanToolbox-v$DisplayVersion-Framework-Dependent.cer"
    releaseApiUri = "https://api.github.com/repos/KiYouJyo/UrbanPlanToolbox/releases/tags/v$DisplayVersion"; checksumFileName = 'SHA256SUMS.txt'
}
$metadata | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $payload 'InstallerMetadata.json') -Encoding UTF8

# User-facing root entry names are an international distribution contract.
# Keep them ASCII/English so the package remains understandable and robust across locales/code pages.
$installEntryName = '1-Install-UrbanPlanToolbox.cmd'
$uninstallEntryName = '2-Uninstall-UrbanPlanToolbox.cmd'
$readmeName = 'README.txt'
$expectedRootFileNames = @($installEntryName, $uninstallEntryName, $readmeName) | Sort-Object
foreach ($sourceName in $expectedRootFileNames) {
    $sourcePath = Join-Path $PSScriptRoot $sourceName
    if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) { throw "Missing one-click root template: $sourceName" }
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot $installEntryName) -Destination (Join-Path $root $installEntryName)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot $uninstallEntryName) -Destination (Join-Path $root $uninstallEntryName)
(Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot $readmeName) -Encoding UTF8).Replace('{{DISPLAY_VERSION}}',$DisplayVersion).Replace('{{PACKAGE_VERSION}}',$PackageVersion) | Set-Content -LiteralPath (Join-Path $root $readmeName) -Encoding UTF8

$actualRootFileNames = @(Get-ChildItem -LiteralPath $root -File | Select-Object -ExpandProperty Name | Sort-Object)
if (($actualRootFileNames -join "`n") -cne ($expectedRootFileNames -join "`n")) { throw "Unexpected one-click root files: $($actualRootFileNames -join ', ')" }
$nonAsciiRootNames = @($actualRootFileNames | Where-Object { $_.ToCharArray() | Where-Object { [int]$_ -gt 127 } })
if ($nonAsciiRootNames.Count -gt 0) { throw "One-click root filenames must be ASCII/English: $($nonAsciiRootNames -join ', ')" }

function Copy-PayloadPowerShellScript([string]$SourceName, [string]$DestinationName) {
    $content = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot $SourceName) -Encoding UTF8
    [IO.File]::WriteAllText((Join-Path $payload $DestinationName), $content, [Text.UTF8Encoding]::new($true))
}
Copy-PayloadPowerShellScript 'payload\InstallAppInstaller.ps1' 'Install.ps1'
Copy-PayloadPowerShellScript 'payload\UninstallAppInstaller.ps1' 'Uninstall.ps1'
Copy-PayloadPowerShellScript 'payload\InstallAppInstallerLauncher.ps1' 'InstallLauncher.ps1'
Copy-PayloadPowerShellScript 'payload\UninstallLauncher.ps1' 'UninstallLauncher.ps1'
Copy-PayloadPowerShellScript 'payload\InstallerMetadataAppInstaller.ps1' 'InstallerMetadata.ps1'
Copy-PayloadPowerShellScript 'payload\ChecksumResolver.ps1' 'ChecksumResolver.ps1'
Copy-PayloadPowerShellScript 'payload\ReleaseDownloadResolver.ps1' 'ReleaseDownloadResolver.ps1'
Copy-Item -LiteralPath $PublicCertificatePath -Destination (Join-Path $payload $metadata.certificateFileName)
$hashLines = Get-ChildItem -LiteralPath $payload -Recurse -File | Where-Object Name -ne 'SHA256SUMS.txt' | Sort-Object FullName | ForEach-Object { "$((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToUpperInvariant()) *$($_.FullName.Substring($payload.Length).TrimStart('\'))" }
Set-Content -LiteralPath (Join-Path $payload 'SHA256SUMS.txt') -Value $hashLines -Encoding UTF8
Write-Output $root
