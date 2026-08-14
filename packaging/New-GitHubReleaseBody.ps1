[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version,
    [Parameter(Mandatory)][string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$tag = "v$Version"
$relativeFiles = @(
    "docs/RELEASE-NOTES-v$Version.md",
    "docs/RELEASE-NOTES-v$Version.ja.md",
    "docs/RELEASE-NOTES-v$Version.en.md"
)
foreach ($relativeFile in $relativeFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $repositoryRoot $relativeFile) -PathType Leaf)) { throw "Required Release Notes sibling is missing: $relativeFile" }
}

$canonicalPath = Join-Path $repositoryRoot $relativeFiles[0]
$canonicalLines = [System.IO.File]::ReadAllLines($canonicalPath, [System.Text.Encoding]::UTF8)
if ($canonicalLines.Count -lt 2 -or $canonicalLines[0] -notmatch '日本語.*English') { throw "Canonical Release Notes do not have the required language switcher: $relativeFiles[0]" }

$repositoryUrl = 'https://github.com/KiYouJyo/UrbanPlanToolbox'
$zhUrl = "$repositoryUrl/blob/$tag/$($relativeFiles[0])"
$jaUrl = "$repositoryUrl/blob/$tag/$($relativeFiles[1])"
$enUrl = "$repositoryUrl/blob/$tag/$($relativeFiles[2])"
$published = "[简体中文]($zhUrl) | [日本語]($jaUrl) | [English]($enUrl)" +
    [Environment]::NewLine + ($canonicalLines[1..($canonicalLines.Count - 1)] -join [Environment]::NewLine)

if ($published -match '(?im)\]\(RELEASE-NOTES-v\d+\.\d+\.\d+\.(?:ja|en)\.md\)') { throw 'Published Release body contains repository-relative language links.' }
foreach ($url in @($zhUrl, $jaUrl, $enUrl)) { if (-not $published.Contains($url)) { throw "Published Release body is missing tag-pinned language URL: $url" } }
$forbidden = @(
    '本候选版本尚未公开发布', '未创建 GitHub Release', 'not publicly released',
    'GitHub Release has not been created', 'この候補版は公開していません', '公開していません'
)
foreach ($phrase in $forbidden) { if ($published.Contains($phrase, [StringComparison]::OrdinalIgnoreCase)) { throw "Published Release body contains candidate-only status: $phrase" } }

$outputDirectory = Split-Path -Parent $OutputPath
if ($outputDirectory) { New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null }
[System.IO.File]::WriteAllText($OutputPath, $published, [System.Text.UTF8Encoding]::new($false))
Write-Host "Generated GitHub Release body: $OutputPath"
