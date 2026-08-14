[CmdletBinding(SupportsShouldProcess)]
param([switch]$Check, [switch]$Apply)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($Check -eq $Apply) { throw 'Specify exactly one of -Check or -Apply.' }

$root = Split-Path -Parent $PSScriptRoot
$metadataPath = Join-Path $root 'release/release.json'
$metadata = [IO.File]::ReadAllText($metadataPath, [Text.UTF8Encoding]::new($false)) | ConvertFrom-Json
$version = [string]$metadata.product.version
$packageVersion = [string]$metadata.product.packageVersion
if ($version -notmatch '^\d+\.\d+\.\d+$' -or $packageVersion -ne "$version.0") { throw 'Release metadata product/package versions are invalid.' }
foreach ($locale in 'zh-CN','ja-JP','en-US') {
    if ([string]::IsNullOrWhiteSpace([string]$metadata.title.$locale)) { throw "Missing title for $locale." }
}
$ids = @($metadata.items | ForEach-Object id)
if ($ids.Count -ne @($ids | Select-Object -Unique).Count) { throw 'Release item IDs must be unique.' }
foreach ($item in $metadata.items) { foreach ($locale in 'zh-CN','ja-JP','en-US') { if ([string]::IsNullOrWhiteSpace([string]$item.$locale)) { throw "Missing item $($item.id) for $locale." } } }

function Set-Generated([string]$Path, [string]$Content) {
    $current = if (Test-Path -LiteralPath $Path) { [IO.File]::ReadAllText($Path, [Text.UTF8Encoding]::new($false)) } else { $null }
    if ($current -ceq $Content) { return }
    if ($Check) { throw "Generated release metadata is stale: $Path" }
    if ($PSCmdlet.ShouldProcess($Path, 'Synchronize release metadata')) { [IO.File]::WriteAllText($Path, $Content, [Text.UTF8Encoding]::new($false)) }
}

$projectPath = Join-Path $root 'UrbanPlanToolbox.csproj'
$project = [IO.File]::ReadAllText($projectPath, [Text.UTF8Encoding]::new($false))
foreach ($pair in @{ '<Version>[^<]+</Version>'="<Version>$version</Version>"; '<AssemblyVersion>[^<]+</AssemblyVersion>'="<AssemblyVersion>$packageVersion</AssemblyVersion>"; '<FileVersion>[^<]+</FileVersion>'="<FileVersion>$packageVersion</FileVersion>"; '<InformationalVersion>[^<]+</InformationalVersion>'="<InformationalVersion>$version</InformationalVersion>" }.GetEnumerator()) { $project = [regex]::Replace($project, $pair.Key, $pair.Value, 1) }
Set-Generated $projectPath $project
foreach ($manifestName in 'Package.appxmanifest','Package.Store.appxmanifest') {
    $path = Join-Path $root $manifestName; $text = [IO.File]::ReadAllText($path, [Text.UTF8Encoding]::new($false))
    $text = [regex]::Replace($text, '(?s)(<Identity\b[^>]*\bVersion=")[^"]+', { param($match) $match.Groups[1].Value + $packageVersion }, 1)
    Set-Generated $path $text
}

$notes = [ordered]@{ schemaVersion = 1; version = $version; notes = [ordered]@{} }
foreach ($locale in 'zh-CN','ja-JP','en-US') { $notes.notes[$locale] = [ordered]@{ title = [string]$metadata.title.$locale; items = @($metadata.items | ForEach-Object { [string]$_.$locale }) } }
$notesJson = ($notes | ConvertTo-Json -Depth 16) + [Environment]::NewLine
Set-Generated (Join-Path $root "Assets/Data/ReleaseNotes/$version.json") $notesJson
Set-Generated (Join-Path $root "docs/release-notes/$version.json") $notesJson
$store = [ordered]@{ version=$version; locales=[ordered]@{} }; foreach ($locale in 'zh-CN','ja-JP','en-US') { $store.locales[$locale] = @($notes.notes[$locale].items) }
Set-Generated (Join-Path $root "packaging/store-release-notes/$version.json") (($store | ConvertTo-Json -Depth 16) + [Environment]::NewLine)

$markdownNames = @{ 'zh-CN' = ''; 'ja-JP' = '.ja'; 'en-US' = '.en' }
foreach ($locale in 'zh-CN','ja-JP','en-US') {
    $items = (@($notes.notes[$locale].items | ForEach-Object { "- $_" }) -join [Environment]::NewLine)
    $links = if ($locale -eq 'zh-CN') { "[$($metadata.languageLabels.'ja-JP')](RELEASE-NOTES-v$version.ja.md) | [$($metadata.languageLabels.'en-US')](RELEASE-NOTES-v$version.en.md)" } elseif ($locale -eq 'ja-JP') { "[$($metadata.languageLabels.'zh-CN')](RELEASE-NOTES-v$version.md) | [$($metadata.languageLabels.'en-US')](RELEASE-NOTES-v$version.en.md)" } else { "[$($metadata.languageLabels.'zh-CN')](RELEASE-NOTES-v$version.md) | [$($metadata.languageLabels.'ja-JP')](RELEASE-NOTES-v$version.ja.md)" }
    $markdown = ("$($metadata.languageLabels.$locale) | $links" + [Environment]::NewLine + [Environment]::NewLine + "# $($notes.notes[$locale].title)" + [Environment]::NewLine + [Environment]::NewLine + $items + [Environment]::NewLine)
    Set-Generated (Join-Path $root "docs/RELEASE-NOTES-v$version$($markdownNames[$locale]).md") $markdown
}

$changelogPath = Join-Path $root 'CHANGELOG.md'
$changelog = [IO.File]::ReadAllText($changelogPath, [Text.UTF8Encoding]::new($false))
$changelogItems = (@($notes.notes['en-US'].items | ForEach-Object { "- $_" }) -join [Environment]::NewLine)
$section = "## $version" + [Environment]::NewLine + [Environment]::NewLine + '### Platform foundation and updater freeze preparation' + [Environment]::NewLine + [Environment]::NewLine + $changelogItems + [Environment]::NewLine + [Environment]::NewLine
$changelog = [regex]::Replace($changelog, "(?ms)^## $([regex]::Escape($version))\r?\n.*?(?=^## |\z)", '')
$firstSection = $changelog.IndexOf('## ', [StringComparison]::Ordinal)
if ($firstSection -lt 1) { throw 'CHANGELOG heading is not in the expected format.' }
$changelog = $changelog.Substring(0, $firstSection) + $section + $changelog.Substring($firstSection)
Set-Generated $changelogPath $changelog
$result = if ($Check) { 'is synchronized' } else { 'synchronized' }
Write-Host "Release metadata ${result}: $version / $packageVersion"
