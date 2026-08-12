[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$OutputPath,
    [Parameter(Mandatory)][string]$DisplayVersion,
    [Parameter(Mandatory)][string]$BundleFileName,
    [string]$BundleUri = "https://github.com/KiYouJyo/UrbanPlanToolbox/releases/download/v$DisplayVersion/$BundleFileName",
    [string]$InstallerUri = 'https://kiyoujyo.github.io/UrbanPlanToolbox/UrbanPlanToolbox.appinstaller'
)

$packageName = '556F80C5-C4D4-452B-93B4-00DE3FA7AC29'
$publisher = 'CN=AppPublisher'
$packageVersion = "$DisplayVersion.0"
if ($DisplayVersion -notmatch '^\d+\.\d+\.\d+$') { throw 'DisplayVersion must be Major.Minor.Patch.' }
if ([IO.Path]::GetExtension($BundleFileName) -notin @('.msixbundle', '.appxbundle')) { throw 'BundleFileName must be an MSIX/AppX bundle.' }

$directory = Split-Path -Parent $OutputPath
if ($directory) { New-Item -ItemType Directory -Force -Path $directory | Out-Null }
$xml = @"
<?xml version="1.0" encoding="utf-8"?>
<AppInstaller xmlns="http://schemas.microsoft.com/appx/appinstaller/2017/2" xmlns:s4="http://schemas.microsoft.com/appx/appinstaller/2021" Version="$packageVersion" Uri="$InstallerUri">
  <MainBundle Name="$packageName" Publisher="$publisher" Version="$packageVersion" Uri="$BundleUri" />
</AppInstaller>
"@
$resolvedPath = $OutputPath
if (Test-Path -LiteralPath $OutputPath) { $resolvedPath = (Resolve-Path -LiteralPath $OutputPath).Path }
[IO.File]::WriteAllText($resolvedPath, $xml, [Text.UTF8Encoding]::new($false))
Write-Output (Resolve-Path -LiteralPath $OutputPath).Path
