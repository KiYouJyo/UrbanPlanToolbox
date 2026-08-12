[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'InstallerMetadata.ps1')
$metadata = Get-InstallerMetadata $PSScriptRoot
$package = Get-AppxPackage -Name $metadata.packageIdentityName -ErrorAction SilentlyContinue | Where-Object Publisher -eq $metadata.publisher | Sort-Object Version -Descending | Select-Object -First 1
if ($package) { Remove-AppxPackage -Package $package.PackageFullName -ErrorAction Stop; Write-Output "Removed $($package.PackageFullName)." } else { Write-Output 'UrbanPlanToolbox is not installed.' }
