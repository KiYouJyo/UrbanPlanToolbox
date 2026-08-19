param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

$ErrorActionPreference = 'Stop'

function Assert-Contains {
    param([string]$Path, [string]$Text)

    $content = [System.IO.File]::ReadAllText($Path, [System.Text.Encoding]::UTF8)
    if (-not $content.Contains($Text)) {
        throw "Documentation check failed: '$Path' does not contain '$Text'."
    }
}

$readmes = @('README.md', 'README.en.md', 'README.ja.md') |
    ForEach-Object { Join-Path $RepositoryRoot $_ }

foreach ($readme in $readmes) {
    Assert-Contains $readme 'github/v/release/KiYouJyo/UrbanPlanToolbox?display_name=tag&sort=semver'
    Assert-Contains $readme 'https://github.com/KiYouJyo/UrbanPlanToolbox/releases/latest'
}

Assert-Contains (Join-Path $RepositoryRoot 'CHANGELOG.md') '## 1.3.0'
Assert-Contains (Join-Path $RepositoryRoot 'docs/index.html') 'project-status.json'
Assert-Contains (Join-Path $RepositoryRoot 'docs/index.html') 'release-notes/${githubVersion}.json'
Assert-Contains (Join-Path $RepositoryRoot 'docs/project-status.json') '"schemaVersion": 2'
Assert-Contains (Join-Path $RepositoryRoot 'docs/ROADMAP.md') 'does not assign unapproved work a release number or date'

$project = [xml][System.IO.File]::ReadAllText((Join-Path $RepositoryRoot 'UrbanPlanToolbox.csproj'), [System.Text.Encoding]::UTF8)
$version = @($project.Project.PropertyGroup | ForEach-Object Version | Where-Object { $_ })[0]
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw "Documentation check failed: unsupported project version '$version'." }
Assert-Contains (Join-Path $RepositoryRoot 'CHANGELOG.md') "## $version"

$markdownFiles = @(
    "docs/RELEASE-NOTES-v$version.md",
    "docs/RELEASE-NOTES-v$version.ja.md",
    "docs/RELEASE-NOTES-v$version.en.md"
)
foreach ($relativePath in $markdownFiles) {
    $path = Join-Path $RepositoryRoot $relativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Documentation check failed: required Release Notes sibling is missing: $relativePath" }
}
$zhNotes = [System.IO.File]::ReadAllText((Join-Path $RepositoryRoot $markdownFiles[0]), [System.Text.Encoding]::UTF8)
$jaNotes = [System.IO.File]::ReadAllText((Join-Path $RepositoryRoot $markdownFiles[1]), [System.Text.Encoding]::UTF8)
$enNotes = [System.IO.File]::ReadAllText((Join-Path $RepositoryRoot $markdownFiles[2]), [System.Text.Encoding]::UTF8)
if ($zhNotes -notmatch "\(RELEASE-NOTES-v$([regex]::Escape($version))\.ja\.md\).*\(RELEASE-NOTES-v$([regex]::Escape($version))\.en\.md\)") { throw 'Documentation check failed: Chinese Release Notes sibling links are invalid.' }
if ($jaNotes -notmatch "\(RELEASE-NOTES-v$([regex]::Escape($version))\.md\).*\(RELEASE-NOTES-v$([regex]::Escape($version))\.en\.md\)") { throw 'Documentation check failed: Japanese Release Notes sibling links are invalid.' }
if ($enNotes -notmatch "\(RELEASE-NOTES-v$([regex]::Escape($version))\.md\).*\(RELEASE-NOTES-v$([regex]::Escape($version))\.ja\.md\)") { throw 'Documentation check failed: English Release Notes sibling links are invalid.' }

& (Join-Path $RepositoryRoot 'packaging/Sync-ReleaseNotes.ps1') -Version $version -Check
if ($LASTEXITCODE -ne 0) { throw 'Documentation check failed: structured Release Notes mirror check failed.' }

$publishedBody = [System.IO.Path]::GetTempFileName()
try {
    & (Join-Path $RepositoryRoot 'packaging/New-GitHubReleaseBody.ps1') -Version $version -OutputPath $publishedBody
    if ($LASTEXITCODE -ne 0) { throw 'Documentation check failed: GitHub Release body generation failed.' }
    $published = [System.IO.File]::ReadAllText($publishedBody, [System.Text.Encoding]::UTF8)
    foreach ($language in @('', '.ja', '.en')) {
        $url = "https://github.com/KiYouJyo/UrbanPlanToolbox/blob/v$version/docs/RELEASE-NOTES-v$version$language.md"
        if (-not $published.Contains($url)) { throw "Documentation check failed: published body does not contain tag-pinned URL '$url'." }
    }
    if ($published -match '(?im)\]\(RELEASE-NOTES-v\d+\.\d+\.\d+\.(?:ja|en)\.md\)') { throw 'Documentation check failed: published body contains repository-relative language links.' }
}
finally {
    Remove-Item -LiteralPath $publishedBody -Force -ErrorAction SilentlyContinue
}

$staleCurrentText = @('The preview version is **v1.3.0**')
foreach ($readme in $readmes) {
    $content = [System.IO.File]::ReadAllText($readme, [System.Text.Encoding]::UTF8)
    foreach ($staleText in $staleCurrentText) {
        if ($content.Contains($staleText)) {
            throw "Documentation check failed: stale current-version text '$staleText' remains in '$readme'."
        }
    }
}

& (Join-Path $RepositoryRoot 'packaging/Test-WebDavPrivacyBoundary.ps1')

Write-Host 'Release documentation consistency checks passed.'
