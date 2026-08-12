[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Version,
    [Parameter(Mandatory)][string]$Path,
    [Parameter(Mandatory)][string]$OutputPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$source = Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
if (-not $source.PSObject.Properties['version'] -or [string]$source.version -ne $Version) { throw "Release-notes version must be $Version." }
if (-not $source.PSObject.Properties['locales'] -or -not $source.locales) { throw "Release-notes JSON is missing the 'locales' object." }

$normalized = [ordered]@{ Version = $Version; Locales = [ordered]@{} }
foreach ($locale in @('zh-CN','ja-JP','en-US')) {
    $keys = @($source.locales.PSObject.Properties.Name | Where-Object { [string]::Equals([string]$_, $locale, [StringComparison]::OrdinalIgnoreCase) })
    if ($keys.Count -ne 1) { throw "Release-notes JSON must contain exactly one '$locale' entry." }
    $localeValue = $source.locales.PSObject.Properties[$keys[0]].Value
    $lines = foreach ($item in @($localeValue)) {
        $line = ([string]$item).Trim()
        if ([string]::IsNullOrWhiteSpace($line) -or $line.Contains("`r") -or $line.Contains("`n")) { throw "Each release-note item for $locale must be one non-empty line." }
        "- $line"
    }
    if (@($lines).Count -eq 0) { throw "Release notes for $locale are empty." }
    $text = $lines -join "`n"
    if ($text.Length -gt 1500) { throw "Release notes for $locale exceed the 1500-character Store limit." }
    $normalized.Locales[$locale] = $text
    Write-Host "$locale release notes: $($text.Length) characters"
}

$parent = Split-Path -Parent $OutputPath
if ($parent) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
$json = $normalized | ConvertTo-Json -Depth 10
[System.IO.File]::WriteAllText($OutputPath, $json, [System.Text.UTF8Encoding]::new($false))
