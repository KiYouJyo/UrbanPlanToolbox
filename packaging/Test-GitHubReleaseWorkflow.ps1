[CmdletBinding()]
param([string]$Path = (Join-Path $PSScriptRoot '..\.github\workflows\publish-github-release.yml'))

$ErrorActionPreference = 'Stop'
$text = Get-Content -Raw -LiteralPath (Resolve-Path $Path)
foreach ($required in @("tags:", "'v*.*.*'", 'workflow_dispatch:', 'dry_run:', 'contents: write', 'refs/tags/v', 'BD85AD77A651C86CA01A480C8E9BC64952993F98', "GetEnvironmentVariable('ProgramFiles(x86)')", 'timestamp.sectigo.com', 'Create or reconcile GitHub Release', 'gh release create', '--notes-file', 'actions/upload-artifact@v4')) {
    if ($text -notmatch [regex]::Escape($required)) { throw "Workflow is missing required contract: $required" }
}
if ($text -match '\$env:ProgramFiles\(x86\)') { throw 'Workflow contains the invalid PowerShell ProgramFiles(x86) expression.' }
if ($text -match 'gh release delete|gh api -X DELETE') { throw 'Workflow must never delete Releases or assets.' }
if ($text -match '-f body=') { throw 'Release notes must be passed through --notes-file, not an inline API body.' }
Write-Output 'GitHub release workflow contract validation passed.'
