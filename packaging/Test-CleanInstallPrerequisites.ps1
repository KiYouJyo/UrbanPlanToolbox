[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ExpectedPackageFamilyName,
    [string]$PackageDataRoot,
    [string]$LegacyDataRoot = (Join-Path $env:LOCALAPPDATA 'UrbanPlanToolbox')
)

$ErrorActionPreference = 'Stop'
$PackageDataRoot = if ($PackageDataRoot) { $PackageDataRoot } else { Join-Path $env:LOCALAPPDATA "Packages\$ExpectedPackageFamilyName" }

$installed = @(Get-AppxPackage -ErrorAction SilentlyContinue | Where-Object { $_.PackageFamilyName -eq $ExpectedPackageFamilyName })
if ($installed.Count -gt 0) {
    throw "The package is still installed: $($installed.PackageFullName -join ', ')"
}

$packageFiles = if (Test-Path -LiteralPath $PackageDataRoot -PathType Container) {
    @(Get-ChildItem -LiteralPath $PackageDataRoot -Recurse -File -ErrorAction SilentlyContinue)
} else { @() }
if ($packageFiles.Count -gt 0) {
    throw "Package data remains under $PackageDataRoot. Do not classify this as a clean install."
}

# The installer deliberately preserves the package-external user data root.
# A clean-install test must explicitly use an isolated/cleared root; never
# delete it here because it may contain the user's real projects and settings.
$legacyEvidence = @()
if (Test-Path -LiteralPath $LegacyDataRoot -PathType Container) {
    $legacyEvidence = @(Get-ChildItem -LiteralPath $LegacyDataRoot -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -in @('settings.json', 'first-run-guide.json') -or $_.FullName -match '\\(data|attachments)\\' })
}
if ($legacyEvidence.Count -gt 0) {
    $paths = $legacyEvidence.FullName -join [Environment]::NewLine
    throw "Package-external historical data remains. Use an isolated data root or restore it only for upgrade validation:`n$paths"
}

[pscustomobject]@{
    PackageFamilyName = $ExpectedPackageFamilyName
    PackageRemoved = $true
    PackageDataCleared = $true
    PackageExternalHistoricalDataCleared = $true
    ValidationResult = 'Passed'
}
