[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$script:fakeBytes = 'urban-plan-toolbox-bundle'
$script:bitsCalls = 0
$script:webCalls = 0
$script:webMode = 'success'
function Start-Sleep { param([int]$Seconds) }
function Start-BitsTransfer {
    param([string]$Source,[string]$Destination,[int]$RetryInterval,[int]$RetryTimeout)
    $script:bitsCalls++
    [IO.File]::WriteAllText($Destination, $script:fakeBytes)
}
function Invoke-WebRequest {
    param([string]$Uri,[switch]$UseBasicParsing,[string]$OutFile)
    $script:webCalls++
    if ($script:webMode -eq 'first-eof' -and $script:webCalls -eq 1) { throw [IO.IOException]::new('Received an unexpected EOF or 0 bytes from the transport stream.') }
    if ($script:webMode -eq 'always-eof') { throw [IO.IOException]::new('Received an unexpected EOF or 0 bytes from the transport stream.') }
    [IO.File]::WriteAllText($OutFile, $script:fakeBytes)
}
. (Join-Path $PSScriptRoot 'payload\ReleaseDownloadResolver.ps1')
$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('UrbanPlanToolbox-download-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null
try {
    $logLines = [Collections.Generic.List[string]]::new()
    $log = { param([string]$Message) $logLines.Add($Message) }
    $destination = Join-Path $tempRoot 'bundle.msixbundle'

    Download-ReleaseAssetRobust -Uri 'https://example.invalid/bundle' -Destination $destination -ExpectedBytes $script:fakeBytes.Length -ReleaseTag 'v1.5.8' -AssetName 'bundle.msixbundle' -Log $log
    if ($script:bitsCalls -ne 1 -or $script:webCalls -ne 0) { throw 'BITS success did not avoid fallback.' }
    if (-not ($logLines -contains 'BundleDownloadCompleted')) { throw 'BITS success did not log completion.' }

    $script:webCalls = 0; $script:webMode = 'first-eof'
    Remove-Item -LiteralPath $destination -Force
    Download-ReleaseAssetRobust -Uri 'https://example.invalid/bundle' -Destination $destination -ExpectedBytes $script:fakeBytes.Length -ReleaseTag 'v1.5.8' -AssetName 'bundle.msixbundle' -Log $log -DisableBits
    if ($script:webCalls -ne 2) { throw "EOF retry expected two fallback attempts, got $script:webCalls." }

    $script:webCalls = 0; $script:webMode = 'always-eof'
    $failed = $false
    try { Download-ReleaseAssetRobust -Uri 'https://example.invalid/bundle' -Destination $destination -ExpectedBytes $script:fakeBytes.Length -ReleaseTag 'v1.5.8' -AssetName 'bundle.msixbundle' -Log $log -DisableBits } catch { $failed = $_.Exception.Message -eq 'DownloadFailed' }
    if (-not $failed -or $script:webCalls -ne 3 -or (Test-Path -LiteralPath $destination)) { throw 'Three transient fallback failures did not fail cleanly.' }

    $script:webCalls = 0; $script:webMode = 'success'
    [IO.File]::WriteAllText($destination, 'partial')
    $sizeFailed = $false
    try { Download-ReleaseAssetRobust -Uri 'https://example.invalid/bundle' -Destination $destination -ExpectedBytes ($script:fakeBytes.Length + 1) -ReleaseTag 'v1.5.8' -AssetName 'bundle.msixbundle' -Log $log -DisableBits } catch { $sizeFailed = $_.Exception.Message -eq 'DownloadFailed' }
    if (-not $sizeFailed -or (Test-Path -LiteralPath $destination)) { throw 'Size mismatch did not fail as IncompleteDownload.' }

    Write-Output 'Release download resolver tests passed.'
}
finally { if (Test-Path -LiteralPath $tempRoot) { Remove-Item -LiteralPath $tempRoot -Recurse -Force } }
