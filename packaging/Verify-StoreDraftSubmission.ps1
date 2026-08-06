[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ProductId,
    [Parameter(Mandatory)][string]$ExpectedPackageVersion,
    [Parameter(Mandatory)][string]$ExpectedPackageFileName,
    [Parameter(Mandatory)][string]$ExpectedReleaseNotesPath,
    [Parameter(Mandatory)][string]$ExpectedSubmissionId,
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

function Get-Value {
    param([System.Collections.IDictionary]$Dictionary,[string]$Name)
    return $Dictionary[(Get-Key -Dictionary $Dictionary -Name $Name)]
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
if ($pending -isnot [System.Collections.IDictionary]) { throw 'The Store application does not contain a pending draft submission.' }

$submissionId = Get-Text (Get-Value -Dictionary $pending -Name 'Id')
if ($submissionId -ne $ExpectedSubmissionId) { throw "Pending submission ID mismatch. Expected=$ExpectedSubmissionId Actual=$submissionId" }
$submissionUri = "$applicationUri/submissions/$submissionId"
$submission = (Invoke-WebRequest -Method Get -Uri $submissionUri -Headers $headers).Content | ConvertFrom-Json -AsHashtable -Depth 100

$status = Get-Text (Get-Value -Dictionary $submission -Name 'Status')
if ($status -cne 'PendingCommit') { throw "Expected Store draft status PendingCommit; actual '$status'." }

$packages = @(Get-Value -Dictionary $submission -Name 'ApplicationPackages')
if ($packages.Count -eq 0) { throw 'Store draft application package list is empty.' }
$expectedVersionPackages = @($packages | Where-Object {
    $_ -is [System.Collections.IDictionary] -and (Get-Text (Get-OptionalValue -Dictionary $_ -Name 'Version')) -eq $ExpectedPackageVersion
})
if ($expectedVersionPackages.Count -eq 0) { throw "Store draft package list does not contain version $ExpectedPackageVersion." }

$expectedStem = [IO.Path]::GetFileNameWithoutExtension($ExpectedPackageFileName).ToLowerInvariant().Replace('_bundle', '')
$matchingPackage = @($expectedVersionPackages | Where-Object {
    $fileName = (Get-Text (Get-OptionalValue -Dictionary $_ -Name 'FileName')).ToLowerInvariant()
    $fileStem = [IO.Path]::GetFileNameWithoutExtension($fileName).Replace('_bundle', '')
    $fileStem -eq $expectedStem -or $fileName -eq $ExpectedPackageFileName.ToLowerInvariant()
})
if ($matchingPackage.Count -eq 0) { throw "Store draft package list does not contain the uploaded package '$ExpectedPackageFileName'." }

$architectures = @($matchingPackage | ForEach-Object { Get-Text (Get-OptionalValue -Dictionary $_ -Name 'Architecture') } | Where-Object { $_ })
if ($architectures.Count -gt 0 -and @($architectures | Where-Object { $_ -ine 'X64' }).Count -gt 0) { throw "Store draft package architecture is not x64: $($architectures -join ', ')." }

$notes = Get-Content -LiteralPath $ExpectedReleaseNotesPath -Raw | ConvertFrom-Json -AsHashtable -Depth 20
$notesLocales = Get-Value -Dictionary $notes -Name 'Locales'
$listings = Get-Value -Dictionary $submission -Name 'Listings'
if ($listings -isnot [System.Collections.IDictionary]) { throw 'The Store draft does not contain a Listings dictionary.' }
foreach ($locale in @('zh-CN','ja-JP','en-US')) {
    $listing = $listings[(Get-Key -Dictionary $listings -Name $locale)]
    $baseListing = Get-Value -Dictionary $listing -Name 'BaseListing'
    $actual = Get-Text (Get-Value -Dictionary $baseListing -Name 'ReleaseNotes')
    $expected = Get-Text $notesLocales[(Get-Key -Dictionary $notesLocales -Name $locale)]
    if ($actual -cne $expected) { throw "Store release-notes verification failed for $locale." }
    Write-Host "Verified Store release notes for $locale."
}

$packageNames = @($matchingPackage | ForEach-Object { Get-Text (Get-OptionalValue -Dictionary $_ -Name 'FileName') })
Write-Output "submission_id=$submissionId"
Write-Output "submission_status=$status"
Write-Output "package_version=$ExpectedPackageVersion"
Write-Output "package_file=$ExpectedPackageFileName"
Write-Output "verified_package_names=$($packageNames -join ',')"
Write-Output 'release_notes_verified=zh-CN,ja-JP,en-US'
