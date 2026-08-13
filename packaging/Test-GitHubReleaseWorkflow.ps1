[CmdletBinding()]
param([string]$Path = (Join-Path $PSScriptRoot '..\.github\workflows\publish-github-release.yml'))

$ErrorActionPreference = 'Stop'
$text = Get-Content -Raw -LiteralPath (Resolve-Path $Path)
foreach ($required in @("tags:", "'v*.*.*'", "'^v\d+\.\d+\.\d+$'", 'Substring(1)', 'workflow_dispatch:', 'dry_run:', 'contents: write', 'timeout-minutes: 45', 'concurrency:', 'cancel-in-progress: false', 'refs/tags/v', 'BD85AD77A651C86CA01A480C8E9BC64952993F98', "GetEnvironmentVariable('ProgramFiles(x86)')", 'timestamp.digicert.com', 'Get-AuthenticodeSignature', 'SignerCertificate', 'StatusMessage', "Status -eq 'UnknownError'", 'Create or reconcile GitHub Release', 'gh release create', 'gh release upload', 'Verify published GitHub Release', '--notes-file', 'actions/upload-artifact@v7')) {
    if ($text -notmatch [regex]::Escape($required)) { throw "Workflow is missing required contract: $required" }
}
if ($text -match '\$env:ProgramFiles\(x86\)') { throw 'Workflow contains the invalid PowerShell ProgramFiles(x86) expression.' }
if ($text -match 'Import-Certificate|certutil.exe') { throw 'Workflow must not mutate the runner certificate store.' }
if ($text -match "TrimStart\('v'\)") { throw "Workflow must not use TrimStart('v') for tag validation." }
if ($text -match "@\('Valid',\s*'Unknown'") { throw 'Unknown must not be accepted as a successful Authenticode status.' }
if ($text -match '(?ms)publish:\s*\r?\n\s+env:\s*\r?\n\s+RELEASE_CERTIFICATE') { throw 'Release certificate secrets must not be job-level environment variables.' }
if ($text -notmatch '(?ms)Validate signing configuration.*?env:\s*\r?\n\s+RELEASE_CERTIFICATE_BASE64') { throw 'Signing validation step must receive secrets explicitly.' }
if ($text -notmatch '(?ms)Sign bundle and export public certificate.*?env:\s*\r?\n\s+RELEASE_CERTIFICATE_BASE64') { throw 'Signing step must receive secrets explicitly.' }
if ($text -notmatch '(?ms)gh release upload.*?LASTEXITCODE') { throw 'Release asset upload must check LASTEXITCODE.' }
if ($text -match 'gh release delete|gh api -X DELETE') { throw 'Workflow must never delete Releases or assets.' }
if ($text -match '-f body=') { throw 'Release notes must be passed through --notes-file, not an inline API body.' }
Write-Output 'GitHub release workflow contract validation passed.'
