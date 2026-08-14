[CmdletBinding()]
param(
    [AllowEmptyCollection()]
    [string[]]$ChangedPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$safeRootMarkdown = @(
    'CHANGELOG.md',
    'CONTRIBUTING.md',
    'PRIVACY.md',
    'README.md',
    'README.en.md',
    'README.ja.md',
    'ROADMAP.md',
    'SUPPORT.md',
    'THIRD-PARTY-NOTICES.md'
)

$safeExactDocumentation = @(
    'docs/project-status.json',
    'docs/update-manifest.json'
)

function New-Classification {
    param(
        [ValidateSet('full', 'lightweight')]
        [string]$Scope,
        [string]$Reason,
        [string[]]$Files
    )

    [pscustomobject]@{
        Scope  = $Scope
        Reason = $Reason
        Files  = @($Files)
    }
}

$paths = @($ChangedPath | ForEach-Object {
    if ($null -eq $_) { return $null }
    $_.Trim().Replace('\\', '/')
})

if ($paths.Count -eq 0) {
    return New-Classification -Scope full -Reason 'no changed files were available for reliable classification' -Files @()
}

$unsafePaths = [System.Collections.Generic.List[string]]::new()
foreach ($path in $paths) {
    if ([string]::IsNullOrWhiteSpace($path)) {
        $unsafePaths.Add('<empty or unresolved path>')
        continue
    }

    $isSafeDocumentation = $safeExactDocumentation -contains $path -or
        $path -match '^docs/.+\.(md|html)$' -or
        $safeRootMarkdown -contains $path

    if (-not $isSafeDocumentation) {
        $unsafePaths.Add($path)
    }
}

if ($unsafePaths.Count -gt 0) {
    return New-Classification -Scope full -Reason 'full CI required because one or more changed files are outside the documentation/static-site/status allowlist' -Files $unsafePaths.ToArray()
}

return New-Classification -Scope lightweight -Reason 'all changed files are documentation/static-site/status metadata only' -Files $paths
