[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ProductId,
    [Parameter(Mandatory)][string]$ExpectedLastPublishedSubmissionId,
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
if ($null -ne $pending) { throw 'Store application already has a pending submission; publication stopped before upload.' }

$lastPublished = Get-OptionalValue -Dictionary $application -Name 'LastPublishedApplicationSubmission'
if ($lastPublished -isnot [System.Collections.IDictionary]) { throw 'Store application does not contain LastPublishedApplicationSubmission.' }
$lastPublishedId = Get-Text (Get-OptionalValue -Dictionary $lastPublished -Name 'Id')
if ([string]::IsNullOrWhiteSpace($lastPublishedId)) { throw 'LastPublishedApplicationSubmission does not contain an ID.' }
if ($lastPublishedId -cne $ExpectedLastPublishedSubmissionId) {
    throw "Last published submission mismatch. Expected=$ExpectedLastPublishedSubmissionId Actual=$lastPublishedId"
}

Write-Output 'pending_submission=none'
Write-Output "last_published_submission_id=$lastPublishedId"
Write-Output "product_id=$ProductId"
