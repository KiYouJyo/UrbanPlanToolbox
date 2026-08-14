[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$passes = [System.Collections.Generic.List[string]]::new()
$failures = [System.Collections.Generic.List[string]]::new()

function Test-Requirement {
    param([bool]$Condition, [string]$Message)
    if ($Condition) { $passes.Add($Message) } else { $failures.Add($Message) }
}

function Get-Text([string]$RelativePath) {
    $path = Join-Path $repositoryRoot $RelativePath
    Test-Requirement (Test-Path -LiteralPath $path -PathType Leaf) "Required file exists: $RelativePath"
    if (Test-Path -LiteralPath $path -PathType Leaf) { return Get-Content -LiteralPath $path -Raw -Encoding utf8 }
    return ''
}

$versions = Get-Text 'Services/DataContractVersions.cs'
$projectService = Get-Text 'Services/ProjectStorageService.cs'
$migration12 = Get-Text 'Services/ProjectV1ToV2Migration.cs'
$migration23 = Get-Text 'Services/ProjectV2ToV3Migration.cs'
$fixtureReadme = Get-Text 'tests/Fixtures/ProjectSchema/README.md'
$dataStorage = Get-Text 'docs/DATA_STORAGE.md'
$releasePath = Join-Path $repositoryRoot 'release/release.json'

$projectVersion = if ($versions -match 'Project\s*=\s*(\d+)') { [int]$Matches[1] } else { 0 }
$backupVersion = if ($versions -match 'Backup\s*=\s*(\d+)') { [int]$Matches[1] } else { 0 }
Test-Requirement ($projectVersion -ge 1) 'Project schema version is a positive integer'
Test-Requirement ($backupVersion -ge 1) 'Backup format version is a positive integer'
Test-Requirement ($projectService -match 'ProjectSchemaVersion\s*=\s*DataContractVersions\.Project') 'Project storage reads the authoritative project schema version'
Test-Requirement ($versions -match 'independent') 'Data contract source records independent project and backup lifecycles'

try { $release = Get-Content -LiteralPath $releasePath -Raw -Encoding utf8 | ConvertFrom-Json }
catch { $failures.Add("release/release.json cannot be read: $($_.Exception.Message)") }
if ($release) {
    Test-Requirement ($release.compatibility.projectSchemaVersion -eq $projectVersion) 'Release metadata matches project schema version'
    Test-Requirement ($release.compatibility.backupFormatVersion -eq $backupVersion) 'Release metadata matches backup format version'
}

for ($version = 1; $version -le $projectVersion; $version++) {
    $fixture = Join-Path $repositoryRoot "tests/Fixtures/ProjectSchema/v$version/minimal-valid.json"
    Test-Requirement (Test-Path -LiteralPath $fixture -PathType Leaf) "Historical fixture exists for project schema v$version"
    if (Test-Path -LiteralPath $fixture -PathType Leaf) {
        try {
            $json = Get-Content -LiteralPath $fixture -Raw -Encoding utf8 | ConvertFrom-Json
            Test-Requirement ($json.schemaVersion -eq $version) "Fixture v$version declares its matching schema version"
            Test-Requirement ($null -ne $json.payload.id) "Fixture v$version has a persisted project identity"
        }
        catch { $failures.Add("Fixture v$version is not valid JSON: $($_.Exception.Message)") }
    }
}

for ($version = 1; $version -lt $projectVersion; $version++) {
    $content = if ($version -eq 1) { $migration12 } elseif ($version -eq 2) { $migration23 } else { '' }
    Test-Requirement ($content -match "FromVersion\s*=>\s*$version" -and $content -match "ToVersion\s*=>\s*$($version + 1)") "Migration v$version to v$($version + 1) is registered"
}

Test-Requirement ($fixtureReadme -match '83eb240de2f436bf631c41094a6543ad31dcd452') 'Fixture provenance identifies the v1 source commit'
Test-Requirement ($fixtureReadme -match '467417b9cea32996084480a99c96df7240ca6808') 'Fixture provenance identifies the v2 source commit'
Test-Requirement ($fixtureReadme -match '8d74e4d19713bca5ca3266bff2e0ce5cd07366b4') 'Fixture provenance identifies the v3 source commit'
Test-Requirement ($dataStorage -match "ProjectSchemaVersion = $projectVersion") 'Data storage documentation states the current project schema version'
Test-Requirement ($dataStorage -match "BackupFormatVersion = $backupVersion") 'Data storage documentation states the current backup format version'

foreach ($pass in $passes) { Write-Host "PASS: $pass" }
foreach ($failure in $failures) { Write-Host "FAIL: $failure" -ForegroundColor Red }
Write-Host "Data contract evolution: $($passes.Count) PASS, $($failures.Count) FAIL"
if ($failures.Count -gt 0) { exit 1 }
