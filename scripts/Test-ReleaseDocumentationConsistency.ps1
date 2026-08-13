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
Assert-Contains (Join-Path $RepositoryRoot 'docs/index.html') '<h2>1.3.0</h2>'
Assert-Contains (Join-Path $RepositoryRoot 'docs/project-status.json') '"schemaVersion": 1'
Assert-Contains (Join-Path $RepositoryRoot 'docs/ROADMAP.md') 'does not assign unapproved work a release number or date'

$staleCurrentText = @('The preview version is **v1.3.0**')
foreach ($readme in $readmes) {
    $content = [System.IO.File]::ReadAllText($readme, [System.Text.Encoding]::UTF8)
    foreach ($staleText in $staleCurrentText) {
        if ($content.Contains($staleText)) {
            throw "Documentation check failed: stale current-version text '$staleText' remains in '$readme'."
        }
    }
}

Write-Host 'Release documentation consistency checks passed.'
