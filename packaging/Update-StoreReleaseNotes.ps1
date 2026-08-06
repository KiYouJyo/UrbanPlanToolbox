[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ProductId,
    [Parameter(Mandatory)][string]$ReleaseNotesPath,
    [Parameter(Mandatory)][string]$TenantId,
    [Parameter(Mandatory)][string]$ClientId,
    [Parameter(Mandatory)][string]$ClientSecret
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Get-Key {
    param([System.Collections.IDictionary]$Dictionary,[string]$Name,[switch]$AllowMissing)
    $matches = @($Dictionary.Keys | Where-Object { [string]::Equals([string]$_, $Name, [StringComparison]::OrdinalIgnoreCase) })
    if ($matches.Count -eq 0 -and $AllowMissing) { return $null }
    if ($matches.Count -ne 1) { throw "Expected exactly one '$Name' property; found $($matches.Count)." }
    return $matches[0]
}
function Get-Value {
    param([System.Collections.IDictionary]$Dictionary,[string]$Name)
    return $Dictionary[(Get-Key -Dictionary $Dictionary -Name $Name)]
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
$pending = Get-Value -Dictionary $application -Name 'PendingApplicationSubmission'
if ($pending -isnot [System.Collections.IDictionary]) { throw 'The Store application does not contain a pending draft submission.' }
$submissionId = [string](Get-Value -Dictionary $pending -Name 'Id')
if ([string]::IsNullOrWhiteSpace($submissionId)) { throw 'The pending Store submission does not contain an ID.' }

$submissionUri = "$applicationUri/submissions/$submissionId"
$submission = (Invoke-WebRequest -Method Get -Uri $submissionUri -Headers $headers).Content | ConvertFrom-Json -AsHashtable -Depth 100
$notes = Get-Content -LiteralPath $ReleaseNotesPath -Raw | ConvertFrom-Json -AsHashtable -Depth 20
$notesLocales = Get-Value -Dictionary $notes -Name 'Locales'
$listings = Get-Value -Dictionary $submission -Name 'Listings'
if ($listings -isnot [System.Collections.IDictionary]) { throw 'The Store draft does not contain a Listings dictionary.' }

foreach ($locale in @('zh-CN','ja-JP','en-US')) {
    $listing = $listings[(Get-Key -Dictionary $listings -Name $locale)]
    if ($listing -isnot [System.Collections.IDictionary]) { throw "Store listing '$locale' is not an object." }
    $baseListing = Get-Value -Dictionary $listing -Name 'BaseListing'
    if ($baseListing -isnot [System.Collections.IDictionary]) { throw "Store listing '$locale' does not contain BaseListing." }
    $releaseNotes = [string]$notesLocales[(Get-Key -Dictionary $notesLocales -Name $locale)]
    $releaseNotesKey = Get-Key -Dictionary $baseListing -Name 'ReleaseNotes' -AllowMissing
    if ($null -eq $releaseNotesKey) { $baseListing['ReleaseNotes'] = $releaseNotes } else { $baseListing[$releaseNotesKey] = $releaseNotes }
}

$requestBytes = [Text.Encoding]::UTF8.GetBytes(($submission | ConvertTo-Json -Depth 100 -Compress))
$null = Invoke-WebRequest -Method Put -Uri $submissionUri -Headers $headers -ContentType 'application/json; charset=utf-8' -Body $requestBytes
$verified = (Invoke-WebRequest -Method Get -Uri $submissionUri -Headers $headers).Content | ConvertFrom-Json -AsHashtable -Depth 100
$verifiedListings = Get-Value -Dictionary $verified -Name 'Listings'
foreach ($locale in @('zh-CN','ja-JP','en-US')) {
    $listing = $verifiedListings[(Get-Key -Dictionary $verifiedListings -Name $locale)]
    $baseListing = Get-Value -Dictionary $listing -Name 'BaseListing'
    $actual = [string]$baseListing[(Get-Key -Dictionary $baseListing -Name 'ReleaseNotes')]
    $expected = [string]$notesLocales[(Get-Key -Dictionary $notesLocales -Name $locale)]
    if ($actual -cne $expected) { throw "Store release-notes verification failed for $locale." }
    Write-Host "Verified Store release notes for $locale."
}

Write-Output $submissionId
