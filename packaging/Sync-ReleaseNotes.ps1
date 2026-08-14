[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version,
    [switch]$Check
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$canonicalPath = Join-Path $repositoryRoot "Assets/Data/ReleaseNotes/$Version.json"
$mirrorPath = Join-Path $repositoryRoot "docs/release-notes/$Version.json"
$requiredLocales = @('zh-CN', 'ja-JP', 'en-US')

if (-not (Test-Path -LiteralPath $canonicalPath -PathType Leaf)) { throw "Canonical runtime Release Notes were not found: $canonicalPath" }

$canonicalText = [System.IO.File]::ReadAllText($canonicalPath, [System.Text.UTF8Encoding]::new($false))
try { $document = $canonicalText | ConvertFrom-Json } catch { throw "Canonical runtime Release Notes are not valid JSON: $($_.Exception.Message)" }
if ($document.schemaVersion -ne 1) { throw "Canonical runtime Release Notes must use schemaVersion 1; found '$($document.schemaVersion)'." }
if ($document.version -ne $Version) { throw "Canonical runtime Release Notes version mismatch: expected $Version; found '$($document.version)'." }
if ($null -eq $document.notes) { throw 'Canonical runtime Release Notes must contain notes.' }
foreach ($locale in $requiredLocales) {
    $note = $document.notes.$locale
    if ($null -eq $note -or [string]::IsNullOrWhiteSpace($note.title) -or @($note.items).Count -eq 0 -or @($note.items | Where-Object { [string]::IsNullOrWhiteSpace($_) }).Count -gt 0) {
        throw "Canonical runtime Release Notes are not usable for $locale."
    }
}

if ($Check) {
    if (-not (Test-Path -LiteralPath $mirrorPath -PathType Leaf)) { throw "GitHub Pages Release Notes mirror was not found: $mirrorPath" }
    $mirrorText = [System.IO.File]::ReadAllText($mirrorPath, [System.Text.UTF8Encoding]::new($false))
    try { $mirror = $mirrorText | ConvertFrom-Json } catch { throw "GitHub Pages Release Notes mirror is not valid JSON: $($_.Exception.Message)" }
    $canonicalNormalized = $document | ConvertTo-Json -Depth 32 -Compress
    $mirrorNormalized = $mirror | ConvertTo-Json -Depth 32 -Compress
    if ($canonicalNormalized -cne $mirrorNormalized) { throw "Bundled and GitHub Pages Release Notes differ semantically for $Version." }
    Write-Host "Release Notes mirror is semantically identical for $Version."
    exit 0
}

if ($PSCmdlet.ShouldProcess($mirrorPath, "Synchronize Release Notes $Version from runtime canonical source")) {
    [System.IO.File]::WriteAllText($mirrorPath, $canonicalText, [System.Text.UTF8Encoding]::new($false))
    Write-Host "Synchronized GitHub Pages Release Notes mirror: $mirrorPath"
}
