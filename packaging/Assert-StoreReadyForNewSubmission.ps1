[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ProductId,
    [Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+\.\d+$')][string]$ExpectedPackageVersion,
    [Parameter(Mandatory)][string]$TenantId,
    [Parameter(Mandatory)][string]$ClientId,
    [Parameter(Mandatory)][string]$ClientSecret
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Get-OptionalValue {
    param([System.Collections.IDictionary]$Dictionary,[string]$Name)
    $matches = @($Dictionary.Keys | Where-Object { [string]::Equals([string]$_, $Name, [StringComparison]::OrdinalIgnoreCase) })
    if ($matches.Count -eq 0) { return $null }
    if ($matches.Count -ne 1) { throw "Expected at most one '$Name' property; found $($matches.Count)." }
    return $Dictionary[$matches[0]]
}

function Get-Text {
    param($Value)
    if ($null -eq $Value) { return '' }
    return ([string]$Value).Trim()
}

function Get-PackageVersion {
    param([System.Collections.IDictionary]$Package)
    foreach ($name in @('Version','PackageVersion','PackageVersionString')) {
        $value = Get-OptionalValue -Dictionary $Package -Name $name
        if (-not [string]::IsNullOrWhiteSpace([string]$value)) { return (Get-Text $value) }
    }
    $fileName = Get-Text (Get-OptionalValue -Dictionary $Package -Name 'FileName')
    if ($fileName -match '(?i)_(\d+\.\d+\.\d+\.\d+)(?:_|\.|$)') { return $matches[1] }
    return ''
}

$token = Invoke-RestMethod -Method Post -Uri "https://login.microsoftonline.com/$TenantId/oauth2/v2.0/token" -ContentType 'application/x-www-form-urlencoded' -Body @{
    client_id = $ClientId
    client_secret = $ClientSecret
    scope = 'https://manage.devcenter.microsoft.com/.default'
    grant_type = 'client_credentials'
}
if ([string]::IsNullOrWhiteSpace([string]$token.access_token)) { throw 'Microsoft Entra token response did not contain an access token.' }

$headers = @{ Authorization = "Bearer $($token.access_token)"; TenantId = $TenantId; Accept = 'application/json' }
$applicationUri = "https://manage.devcenter.microsoft.com/v1.0/my/applications/$ProductId"
$application = (Invoke-WebRequest -Method Get -Uri $applicationUri -Headers $headers).Content | ConvertFrom-Json -AsHashtable -Depth 100
$pending = Get-OptionalValue -Dictionary $application -Name 'PendingApplicationSubmission'
if ($null -ne $pending) { throw 'Store application already has a pending submission; publication stopped before upload. Resume or remove that exact draft before starting a new publication.' }

$lastPublished = Get-OptionalValue -Dictionary $application -Name 'LastPublishedApplicationSubmission'
if ($lastPublished -isnot [System.Collections.IDictionary]) { throw 'Store application does not contain LastPublishedApplicationSubmission.' }
$lastPublishedId = Get-Text (Get-OptionalValue -Dictionary $lastPublished -Name 'Id')
if ([string]::IsNullOrWhiteSpace($lastPublishedId)) { throw 'LastPublishedApplicationSubmission does not contain an ID.' }

$lastPublishedUri = "$applicationUri/submissions/$lastPublishedId"
$lastPublishedSubmission = (Invoke-WebRequest -Method Get -Uri $lastPublishedUri -Headers $headers).Content | ConvertFrom-Json -AsHashtable -Depth 100
$packages = @(Get-OptionalValue -Dictionary $lastPublishedSubmission -Name 'ApplicationPackages')
if ($packages.Count -eq 0) { throw 'The last published Store submission does not contain application packages.' }

$publishedVersions = foreach ($package in $packages) {
    if ($package -isnot [System.Collections.IDictionary]) { continue }
    $text = Get-PackageVersion -Package $package
    $parsed = $null
    if (-not [string]::IsNullOrWhiteSpace($text) -and [Version]::TryParse($text, [ref]$parsed)) { $parsed }
}
if (@($publishedVersions).Count -eq 0) { throw 'Unable to determine the package version of the last published Store submission.' }
$lastPublishedPackageVersion = @($publishedVersions | Sort-Object -Descending | Select-Object -First 1)[0]
$expectedVersion = [Version]$ExpectedPackageVersion
if ($expectedVersion -le $lastPublishedPackageVersion) {
    throw "Store package version must increase monotonically. LastPublished=$lastPublishedPackageVersion ExpectedNew=$expectedVersion"
}

Write-Output 'pending_submission=none'
Write-Output "last_published_submission_id=$lastPublishedId"
Write-Output "last_published_package_version=$lastPublishedPackageVersion"
Write-Output "expected_package_version=$ExpectedPackageVersion"
Write-Output "product_id=$ProductId"
