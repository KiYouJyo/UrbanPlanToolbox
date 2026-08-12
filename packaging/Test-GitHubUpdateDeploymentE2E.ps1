[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$BundlePath,
    [Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+\.\d+$')][string]$ExpectedVersion,
    [string]$ExpectedStoreIdentity = 'JoKiy.UrbanPlanToolbox'
)

$ErrorActionPreference = 'Stop'
$identity = '556F80C5-C4D4-452B-93B4-00DE3FA7AC29'
$publisher = 'CN=AppPublisher'
if (-not (Test-Path -LiteralPath $BundlePath -PathType Leaf)) { throw "Bundle not found: $BundlePath" }
$beforeStore = @(Get-AppxPackage -Name $ExpectedStoreIdentity -ErrorAction SilentlyContinue | Select-Object Name,Publisher,Version,PackageFullName)
$before = @(Get-AppxPackage -Name $identity -ErrorAction SilentlyContinue | Where-Object Publisher -ceq $publisher)
$hash = (Get-FileHash -LiteralPath $BundlePath -Algorithm SHA256).Hash.ToUpperInvariant()
$sw = [Diagnostics.Stopwatch]::StartNew()
try {
    Add-AppxPackage -Path (Resolve-Path -LiteralPath $BundlePath) -ForceApplicationShutdown -ErrorAction Stop
} catch {
    $activity = $_.Exception.PSObject.Properties['ActivityId']
    throw "MSIX deployment failed. ActivityId=$($activity.Value); ErrorText=$($_.Exception.Message)"
} finally { $sw.Stop() }
$after = @(Get-AppxPackage -Name $identity -ErrorAction SilentlyContinue | Where-Object Publisher -ceq $publisher)
if ($after.Count -ne 1 -or [string]$after[0].Version -ne $ExpectedVersion -or [string]$after[0].Status -ne 'Ok') { throw "Deployment verification failed: expected $ExpectedVersion, found $($after | Out-String)" }
$afterStore = @(Get-AppxPackage -Name $ExpectedStoreIdentity -ErrorAction SilentlyContinue | Select-Object Name,Publisher,Version,PackageFullName)
if ((ConvertTo-Json $beforeStore -Compress) -ne (ConvertTo-Json $afterStore -Compress)) { throw 'Store package changed during GitHub deployment.' }
Write-Output "GitHubUpdateDeploymentE2E=PASS"
Write-Output "Package=$($after[0].PackageFullName)"
Write-Output "Version=$($after[0].Version)"
Write-Output "BundleSHA256=$hash"
Write-Output "ElapsedMs=$($sw.ElapsedMilliseconds)"
Write-Output 'Note=This gate validates real MSIX deployment; in-app shutdown and automatic restart remain a manual-release-gate.'
