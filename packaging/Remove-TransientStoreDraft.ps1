[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ProductId,
    [Parameter(Mandatory)][string]$SubmissionId,
    [Parameter(Mandatory)][string]$ExpectedPackageVersion,
    [Parameter(Mandatory)][string]$ExpectedPackageFileName,
    [Parameter(Mandatory)][string]$ProtectedPublishedSubmissionId,
    [Parameter(Mandatory)][string]$TenantId,
    [Parameter(Mandatory)][string]$ClientId,
    [Parameter(Mandatory)][string]$ClientSecret
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Get-Key {
    param([System.Collections.IDictionary]$Dictionary,[string]$Name)
    $matches = @($Dictionary.Keys | Where-Object { [string]::Equals([string]$_, $Name, [StringComparison]::OrdinalIgnoreCase) })
    if ($matches.Count -ne 1) { throw "Expected exactly one '$Name' property; found $($matches.Count)." }
    return $matches[0]
}

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

if ($SubmissionId -cne $ProtectedPublishedSubmissionId) {
    # The comparison is intentionally explicit: this script can never target the published submission.
} else {
    throw "Refusing to delete protected published submission $ProtectedPublishedSubmissionId."
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
if ($pending -isnot [System.Collections.IDictionary]) { throw 'Transient Store draft cleanup requires an existing pending submission.' }
$pendingId = Get-Text (Get-OptionalValue -Dictionary $pending -Name 'Id')
if ($pendingId -cne $SubmissionId) { throw "Transient draft ID mismatch. Expected=$SubmissionId Actual=$pendingId" }

$submissionUri = "$applicationUri/submissions/$SubmissionId"
$submission = (Invoke-WebRequest -Method Get -Uri $submissionUri -Headers $headers).Content | ConvertFrom-Json -AsHashtable -Depth 100
$status = Get-Text $submission[(Get-Key -Dictionary $submission -Name 'Status')]
if ($status -cne 'PendingCommit') { throw "Transient draft cleanup requires PendingCommit; actual '$status'." }
$packages = @(Get-OptionalValue -Dictionary $submission -Name 'ApplicationPackages')
if ($packages.Count -eq 0) { throw 'Transient draft cleanup requires a non-empty application package list.' }
$versionMatch = @($packages | Where-Object { $_ -is [System.Collections.IDictionary] -and (Get-PackageVersion -Package $_) -eq $ExpectedPackageVersion })
if ($versionMatch.Count -eq 0) { throw "Transient draft package version does not match $ExpectedPackageVersion." }
$expectedName = $ExpectedPackageFileName.ToLowerInvariant()
$nameMatch = @($versionMatch | Where-Object { (Get-Text (Get-OptionalValue -Dictionary $_ -Name 'FileName')).ToLowerInvariant() -eq $expectedName })
if ($nameMatch.Count -eq 0) { throw "Transient draft package does not match $ExpectedPackageFileName." }

$null = Invoke-WebRequest -Method Delete -Uri $submissionUri -Headers $headers
Write-Output "deleted_submission_id=$SubmissionId"
