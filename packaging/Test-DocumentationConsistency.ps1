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
$documentationPath = Get-RequiredFile 'docs/DOCUMENTATION.md'
if ($failures.Count -eq 0) {
    try {
        $status = Get-Content -LiteralPath $statusPath -Raw -Encoding utf8 | ConvertFrom-Json
        Test-Requirement ($status.schemaVersion -eq 1) 'project-status.json uses supported schemaVersion 1'
    }
    catch {
        $failures.Add("project-status.json is not valid JSON: $($_.Exception.Message)")
    }
}

if ($status) {
    $projectFile = [xml](Get-Content -LiteralPath (Join-Path $repositoryRoot 'UrbanPlanToolbox.csproj') -Raw)
    $projectVersionNode = $projectFile.SelectSingleNode('/*[local-name()="Project"]/*[local-name()="PropertyGroup"]/*[local-name()="Version"]')
    $projectVersion = if ($projectVersionNode) { $projectVersionNode.InnerText } else { $null }
    Test-Requirement ($status.product.version -eq $projectVersion) 'SSOT product version matches UrbanPlanToolbox.csproj'

    $github = $status.distribution.github
    Test-Requirement ($github.productVersion -eq $status.product.version) 'SSOT GitHub product version matches product version'
    Test-Requirement ($github.packageVersion -eq "$($status.product.version).0") 'SSOT GitHub package version matches product version'

    foreach ($manifestName in @('Package.appxmanifest', 'Package.Store.appxmanifest')) {
        [xml]$manifest = Get-Content -LiteralPath (Join-Path $repositoryRoot $manifestName) -Raw
        $identity = $manifest.SelectSingleNode('/*[local-name()="Package"]/*[local-name()="Identity"]')
        Test-Requirement ($identity.Version -eq $github.packageVersion) "$manifestName matches SSOT package version"
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
