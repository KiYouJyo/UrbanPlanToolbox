[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$failures = [System.Collections.Generic.List[string]]::new()
$passes = [System.Collections.Generic.List[string]]::new()

function Test-Requirement {
    param([bool]$Condition, [string]$Message)

    if ($Condition) {
        $passes.Add($Message)
    }
    else {
        $failures.Add($Message)
    }
}

function Get-RequiredFile {
    param([string]$RelativePath)

    $path = Join-Path $repositoryRoot $RelativePath
    Test-Requirement (Test-Path -LiteralPath $path -PathType Leaf) "Required file exists: $RelativePath"
    return $path
}

$statusPath = Get-RequiredFile 'docs/project-status.json'
$releasePath = Get-RequiredFile 'release/release.json'
$documentationPath = Get-RequiredFile 'docs/DOCUMENTATION.md'
if ($failures.Count -eq 0) {
    try {
        $status = Get-Content -LiteralPath $statusPath -Raw -Encoding utf8 | ConvertFrom-Json
        Test-Requirement ($status.schemaVersion -eq 2) 'project-status.json uses supported schemaVersion 2'
        $release = Get-Content -LiteralPath $releasePath -Raw -Encoding utf8 | ConvertFrom-Json
        Test-Requirement ($release.schemaVersion -eq 1) 'release/release.json uses supported schemaVersion 1'
    }
    catch {
        $failures.Add("project-status.json is not valid JSON: $($_.Exception.Message)")
    }
}

if ($status -and $release) {
    $projectFile = [xml](Get-Content -LiteralPath (Join-Path $repositoryRoot 'UrbanPlanToolbox.csproj') -Raw)
    $projectVersionNode = $projectFile.SelectSingleNode('/*[local-name()="Project"]/*[local-name()="PropertyGroup"]/*[local-name()="Version"]')
    $projectVersion = if ($projectVersionNode) { $projectVersionNode.InnerText } else { $null }
    Test-Requirement ($release.product.version -eq $projectVersion) 'Candidate Release Metadata matches UrbanPlanToolbox.csproj'
    Test-Requirement ($release.product.packageVersion -eq "$projectVersion.0") 'Candidate Release Metadata package version matches product version'
    Test-Requirement ($release.channels.github.publish -is [bool]) 'Candidate Release Metadata declares GitHub publish policy'
    Test-Requirement ($release.channels.microsoftStore.submit -is [bool]) 'Candidate Release Metadata declares Microsoft Store submit policy'

    $github = $status.distribution.github
    Test-Requirement ($github.latestPublishedProductVersion -ne $null) 'SSOT records the latest confirmed GitHub publication separately'
    Test-Requirement ($github.latestPublishedPackageVersion -eq "$($github.latestPublishedProductVersion).0") 'Confirmed GitHub package version matches published product version'
    Test-Requirement ($github.latestPublishedReleaseTag -eq "v$($github.latestPublishedProductVersion)") 'Confirmed GitHub tag matches published product version'

    $store = $status.distribution.microsoftStore
    Test-Requirement ($store.submittedPackageVersion -eq "$($store.submittedProductVersion).0") 'Confirmed Store submitted package version matches product version'
    Test-Requirement (-not ($store.state -eq 'certification-submitted' -and [string]::IsNullOrWhiteSpace($store.submittedProductVersion))) 'Store certification state has a submitted version'

    foreach ($manifestName in @('Package.appxmanifest', 'Package.Store.appxmanifest')) {
        [xml]$manifest = Get-Content -LiteralPath (Join-Path $repositoryRoot $manifestName) -Raw
        $identity = $manifest.SelectSingleNode('/*[local-name()="Package"]/*[local-name()="Identity"]')
        Test-Requirement ($identity.Version -eq $release.product.packageVersion) "$manifestName matches Candidate Release Metadata package version"
    }

    if ($status.distribution.microsoftStore.state -eq 'certification-submitted') {
        $currentDocuments = @(
            'README.md', 'README.en.md', 'README.ja.md', 'docs/ROADMAP.md', 'docs/RELEASE.md',
            'docs/STORE-PUBLISHING.md', 'docs/DOCUMENTATION.md', 'docs/index.html'
        )
        foreach ($relativePath in $currentDocuments) {
            $content = Get-Content -LiteralPath (Join-Path $repositoryRoot $relativePath) -Raw -Encoding utf8
            Test-Requirement (-not ($content -match 'Microsoft Store v?1\.6\.7 (is )?published')) "$relativePath does not claim Store v1.6.7 is published"
        }
    }
}

$readmes = @('README.md', 'README.en.md', 'README.ja.md')
foreach ($relativePath in $readmes) {
    $content = Get-Content -LiteralPath (Join-Path $repositoryRoot $relativePath) -Raw -Encoding utf8
    Test-Requirement (-not ($content -match 'GitHub builds download and install later versions through Windows App Installer')) "$relativePath does not use the retired App Installer updater description"
}

$canonicalDocuments = @(
    'docs/DOCUMENTATION.md', 'docs/ROADMAP.md', 'docs/RELEASE.md', 'docs/RELIABILITY.md',
    'docs/STORE-PUBLISHING.md', 'docs/StoreUpdateTesting.md', 'docs/PROJECT_WORKSPACE.md',
    'docs/DATA_STORAGE.md', 'docs/DATA_BACKUP.md', 'docs/LOCALIZATION.md',
    'docs/INTERACTION_COMPONENTS.md', 'docs/TOOL_DEVELOPMENT_TEMPLATE.md', 'docs/FirstRunGuide.md',
    'docs/MILESTONE_REMINDERS.md', 'docs/SHAPEFILE-COMPATIBILITY.md', 'docs/ASSET-CONVENTIONS.md'
)
foreach ($relativePath in $canonicalDocuments) {
    $directory = Split-Path $relativePath -Parent
    $baseName = [System.IO.Path]::GetFileNameWithoutExtension($relativePath)
    $siblings = @($relativePath, (Join-Path $directory "$baseName.ja.md"), (Join-Path $directory "$baseName.en.md"))
    foreach ($sibling in $siblings) { Get-RequiredFile $sibling | Out-Null }
    if ($siblings | Where-Object { -not (Test-Path -LiteralPath (Join-Path $repositoryRoot $_) -PathType Leaf) }) { continue }
    $zh = Get-Content -LiteralPath (Join-Path $repositoryRoot $siblings[0]) -Raw -Encoding utf8
    $ja = Get-Content -LiteralPath (Join-Path $repositoryRoot $siblings[1]) -Raw -Encoding utf8
    $en = Get-Content -LiteralPath (Join-Path $repositoryRoot $siblings[2]) -Raw -Encoding utf8
    Test-Requirement ($zh -match "\($baseName\.ja\.md\).+\($baseName\.en\.md\)") "$relativePath has Chinese language switcher"
    Test-Requirement ($ja -match "\($baseName\.md\).+\($baseName\.en\.md\)") "$relativePath has Japanese language switcher"
    Test-Requirement ($en -match "\($baseName\.md\).+\($baseName\.ja\.md\)") "$relativePath has English language switcher"
}

$roadmap = Get-Content -LiteralPath (Join-Path $repositoryRoot 'docs/ROADMAP.md') -Raw -Encoding utf8
Test-Requirement (-not ($roadmap -match 'v1\.5\.2 当前发布|v1\.5\.2\s+current release')) 'ROADMAP has no retired v1.5.2 current-release heading'

$storePublishing = Get-Content -LiteralPath (Join-Path $repositoryRoot 'docs/STORE-PUBLISHING.md') -Raw -Encoding utf8
Test-Requirement (-not ($storePublishing -match 'v1\.5\.4 当前状态|v1\.5\.4 current status')) 'STORE-PUBLISHING has no retired v1.5.4 current-status heading'

$documentation = Get-Content -LiteralPath $documentationPath -Raw -Encoding utf8
Test-Requirement ($documentation -match 'project-status\.json') 'DOCUMENTATION declares project-status.json'
Test-Requirement ($documentation -notmatch 'update-manifest\.json.{0,100}(?:SSOT|Single Source of Truth)') 'update-manifest.json is not declared as documentation SSOT'

foreach ($pass in $passes) { Write-Host "PASS: $pass" }
foreach ($failure in $failures) { Write-Host "FAIL: $failure" -ForegroundColor Red }
Write-Host "Documentation consistency: $($passes.Count) PASS, $($failures.Count) FAIL"

if ($failures.Count -gt 0) { exit 1 }
