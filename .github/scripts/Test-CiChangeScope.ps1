[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$classifier = Join-Path $PSScriptRoot 'Get-CiChangeScope.ps1'
$failures = [System.Collections.Generic.List[string]]::new()

function Assert-Scope {
    param([string]$Name, [string[]]$ChangedPath, [string]$ExpectedScope)

    $actual = (& $classifier -ChangedPath $ChangedPath).Scope
    if ($actual -ne $ExpectedScope) {
        $failures.Add("$Name expected $ExpectedScope but got $actual")
    }
    else {
        Write-Host "PASS: $Name => $actual"
    }
}

Assert-Scope -Name 'project status only' -ChangedPath @('docs/project-status.json') -ExpectedScope lightweight
Assert-Scope -Name 'docs Markdown only' -ChangedPath @('docs/RELIABILITY.md') -ExpectedScope lightweight
Assert-Scope -Name 'status plus README' -ChangedPath @('docs/project-status.json', 'README.md') -ExpectedScope lightweight
Assert-Scope -Name 'status plus service' -ChangedPath @('docs/project-status.json', 'Services/AppUpdateService.cs') -ExpectedScope full
Assert-Scope -Name 'view model code' -ChangedPath @('ViewModels/UpdateViewModel.cs') -ExpectedScope full
Assert-Scope -Name 'status plus arbitrary code' -ChangedPath @('docs/project-status.json', 'Models/Project.cs') -ExpectedScope full
Assert-Scope -Name 'test code' -ChangedPath @('tests/UrbanPlanToolbox.Tests/AppUpdateTests.cs') -ExpectedScope full
Assert-Scope -Name 'workflow' -ChangedPath @('.github/workflows/ci.yml') -ExpectedScope full
Assert-Scope -Name 'runtime release notes JSON' -ChangedPath @('docs/release-notes/1.7.0.json') -ExpectedScope full
Assert-Scope -Name 'unknown file' -ChangedPath @('new-file.unknown') -ExpectedScope full
Assert-Scope -Name 'empty diff' -ChangedPath @() -ExpectedScope full

$workflow = Get-Content -LiteralPath (Join-Path $repositoryRoot '.github/workflows/ci.yml') -Raw -Encoding utf8
foreach ($requiredText in @(
    'name: Build and test x64',
    'fetch-depth: 0',
    'github.event.pull_request.base.sha',
    'github.event.pull_request.head.sha',
    'github.event.before',
    'github.sha',
    'CI change scope:',
    "steps.change-scope.outputs.scope == 'full'",
    "steps.change-scope.outputs.has-comparison == 'true'"
)) {
    if (-not $workflow.Contains($requiredText)) {
        $failures.Add("Workflow is missing required change-scope contract: $requiredText")
    }
}

if ($workflow -match 'workflow_dispatch:\s*\r?\n\s+inputs:') {
    $failures.Add('Workflow must not expose a manual CI mode input.')
}

$scopeStep = [regex]::Match($workflow, '(?ms)      - name: Determine CI change scope\r?\n.*?(?=\r?\n      - name: Check diff whitespace)')
if (-not $scopeStep.Success) {
    $failures.Add('Workflow is missing the CI change-scope step.')
}
else {
    $tokens = $null
    $parseErrors = $null
    [System.Management.Automation.Language.Parser]::ParseInput(
        [regex]::Match($scopeStep.Value, '(?ms)        run: \|\r?\n(?<script>(?:          .*\r?\n?)*)').Groups['script'].Value -replace '(?m)^          ',
        [ref]$tokens,
        [ref]$parseErrors
    ) | Out-Null
    if ($parseErrors.Count -gt 0) {
        $failures.Add("Workflow change-scope PowerShell has syntax errors: $($parseErrors.Message -join '; ')")
    }
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Host "FAIL: $_" -ForegroundColor Red }
    exit 1
}

Write-Host 'CI change-scope classifier checks passed.'
