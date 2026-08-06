[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ProductId,
    [Parameter(Mandatory)][string]$SubmissionId,
    [Parameter(Mandatory)][string]$ExpectedPackageVersion,
    [Parameter(Mandatory)][string]$ExpectedPackageFileName,
    [Parameter(Mandatory)][string]$TenantId,
    [Parameter(Mandatory)][string]$ClientId,
    [Parameter(Mandatory)][string]$ClientSecret,
    [int]$PollIntervalSeconds = 5,
    [int]$TimeoutSeconds = 60
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
$lastPublished = Get-OptionalValue -Dictionary $application -Name 'LastPublishedApplicationSubmission'
if ($lastPublished -isnot [System.Collections.IDictionary]) { throw 'Transient draft cleanup could not identify the current published submission.' }
$lastPublishedId = Get-Text (Get-OptionalValue -Dictionary $lastPublished -Name 'Id')
if ([string]::IsNullOrWhiteSpace($lastPublishedId)) { throw 'The current published submission does not contain an ID.' }
if ($SubmissionId -ceq $lastPublishedId) { throw "Refusing to delete current published submission $lastPublishedId." }

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
$nameMatch = @($versionMatch | Where-Object {
    $actualName = (Get-Text (Get-OptionalValue -Dictionary $_ -Name 'FileName')).ToLowerInvariant()
    $actualStem = [IO.Path]::GetFileNameWithoutExtension($actualName).Replace('_bundle', '')
    $expectedStem = [IO.Path]::GetFileNameWithoutExtension($expectedName).Replace('_bundle', '')
    $actualName -eq $expectedName -or $actualStem -eq $expectedStem
})
if ($nameMatch.Count -ne 1) { throw "Transient draft package does not uniquely match $ExpectedPackageFileName." }

$null = Invoke-WebRequest -Method Delete -Uri $submissionUri -Headers $headers
$deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
do {
    $after = (Invoke-WebRequest -Method Get -Uri $applicationUri -Headers $headers).Content | ConvertFrom-Json -AsHashtable -Depth 100
    $remaining = Get-OptionalValue -Dictionary $after -Name 'PendingApplicationSubmission'
    if ($null -eq $remaining) { break }
    if ($remaining -is [System.Collections.IDictionary]) {
        $remainingId = Get-Text (Get-OptionalValue -Dictionary $remaining -Name 'Id')
        if ($remainingId -cne $SubmissionId) { throw "A different pending submission appeared during cleanup: $remainingId" }
    }
    if ([DateTimeOffset]::UtcNow -ge $deadline) { throw "Transient Store draft $SubmissionId was not removed within $TimeoutSeconds seconds." }
    Start-Sleep -Seconds $PollIntervalSeconds
} while ($true)

Write-Output "deleted_submission_id=$SubmissionId"
Write-Output "protected_published_submission_id=$lastPublishedId"
