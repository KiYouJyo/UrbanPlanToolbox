[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ProductId,
    [Parameter(Mandatory)][string]$ExpectedSubmissionId,
    [Parameter(Mandatory)][string]$TenantId,
    [Parameter(Mandatory)][string]$ClientId,
    [Parameter(Mandatory)][string]$ClientSecret,
    [int]$PollIntervalSeconds = 15,
    [int]$TimeoutSeconds = 300
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Get-Key {
    param([System.Collections.IDictionary]$Dictionary,[string]$Name)
    $matches = @($Dictionary.Keys | Where-Object { [string]::Equals([string]$_, $Name, [StringComparison]::OrdinalIgnoreCase) })
    if ($matches.Count -ne 1) { throw "Expected exactly one '$Name' property; found $($matches.Count)." }
    return $matches[0]
}

function Get-Text {
    param($Value)
    if ($null -eq $Value) { return '' }
    return ([string]$Value).Trim()
}

$token = Invoke-RestMethod -Method Post -Uri "https://login.microsoftonline.com/$TenantId/oauth2/v2.0/token" -ContentType 'application/x-www-form-urlencoded' -Body @{
    client_id = $ClientId
    client_secret = $ClientSecret
    scope = 'https://manage.devcenter.microsoft.com/.default'
    grant_type = 'client_credentials'
}
if ([string]::IsNullOrWhiteSpace([string]$token.access_token)) { throw 'Microsoft Entra token response did not contain an access token.' }

$headers = @{ Authorization = "Bearer $($token.access_token)"; TenantId = $TenantId; Accept = 'application/json' }
$submissionUri = "https://manage.devcenter.microsoft.com/v1.0/my/applications/$ProductId/submissions/$ExpectedSubmissionId"
$terminalFailureStatuses = @('Cancelled','Canceled','Failed','Rejected','CommitFailed')
$deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
$status = ''
do {
    $submission = (Invoke-WebRequest -Method Get -Uri $submissionUri -Headers $headers).Content | ConvertFrom-Json -AsHashtable -Depth 100
    $actualId = Get-Text $submission[(Get-Key -Dictionary $submission -Name 'Id')]
    if ($actualId -cne $ExpectedSubmissionId) { throw "Committed submission ID mismatch. Expected=$ExpectedSubmissionId Actual=$actualId" }
    $status = Get-Text $submission[(Get-Key -Dictionary $submission -Name 'Status')]
    if ([string]::IsNullOrWhiteSpace($status)) { throw 'Committed Store submission returned an empty status.' }
    Write-Host "Submission $ExpectedSubmissionId status: $status"
    if ($status -cne 'PendingCommit') { break }
    if ([DateTimeOffset]::UtcNow -ge $deadline) { throw "Store submission remained PendingCommit for $TimeoutSeconds seconds." }
    Start-Sleep -Seconds $PollIntervalSeconds
} while ($true)

if ($terminalFailureStatuses -contains $status) { throw "Store submission entered failure status '$status'." }
if ($status -ceq 'PendingCommit') { throw 'Store submission is still PendingCommit.' }

Write-Output "submission_id=$ExpectedSubmissionId"
Write-Output "post_commit_status=$status"
