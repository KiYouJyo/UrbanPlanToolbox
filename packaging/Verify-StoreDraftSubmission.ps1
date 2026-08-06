[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ProductId,
    [Parameter(Mandatory)][string]$ExpectedPackageVersion,
    [Parameter(Mandatory)][string]$ExpectedPackageFileName,
    [Parameter(Mandatory)][string]$ExpectedReleaseNotesPath,
    [Parameter(Mandatory)][string]$ExpectedSubmissionId,
    [Parameter(Mandatory)][string]$TenantId,
    [Parameter(Mandatory)][string]$ClientId,
    [Parameter(Mandatory)][string]$ClientSecret,
    [int]$PollIntervalSeconds = 10,
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

function Test-ExpectedPackageName {
    param([System.Collections.IDictionary]$Package,[string]$ExpectedName)
    $fileName = (Get-Text (Get-OptionalValue -Dictionary $Package -Name 'FileName')).ToLowerInvariant()
    $expectedLower = $ExpectedName.ToLowerInvariant()
    $expectedStem = [IO.Path]::GetFileNameWithoutExtension($expectedLower).Replace('_bundle', '')
    $fileStem = [IO.Path]::GetFileNameWithoutExtension($fileName).Replace('_bundle', '')
    return $fileStem -eq $expectedStem -or $fileName -eq $expectedLower
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
$deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
$submission = $null
$matchingPackage = @()
$packages = @()
do {
    $application = (Invoke-WebRequest -Method Get -Uri $applicationUri -Headers $headers).Content | ConvertFrom-Json -AsHashtable -Depth 100
    $pending = Get-OptionalValue -Dictionary $application -Name 'PendingApplicationSubmission'
    if ($pending -isnot [System.Collections.IDictionary]) {
        if ([DateTimeOffset]::UtcNow -ge $deadline) { throw 'The Store application does not contain a pending draft submission.' }
        Start-Sleep -Seconds $PollIntervalSeconds
        continue
    }

    $submissionId = Get-Text (Get-Value -Dictionary $pending -Name 'Id')
    if ($submissionId -ne $ExpectedSubmissionId) { throw "Pending submission ID mismatch. Expected=$ExpectedSubmissionId Actual=$submissionId" }
    $submissionUri = "$applicationUri/submissions/$submissionId"
    $submission = (Invoke-WebRequest -Method Get -Uri $submissionUri -Headers $headers).Content | ConvertFrom-Json -AsHashtable -Depth 100

    $status = Get-Text (Get-Value -Dictionary $submission -Name 'Status')
    if ($status -cne 'PendingCommit') { throw "Expected Store draft status PendingCommit; actual '$status'." }

    $packages = @(Get-Value -Dictionary $submission -Name 'ApplicationPackages')
    $matchingPackage = @($packages | Where-Object {
        $_ -is [System.Collections.IDictionary] -and
        (Get-PackageVersion -Package $_) -eq $ExpectedPackageVersion -and
        (Test-ExpectedPackageName -Package $_ -ExpectedName $ExpectedPackageFileName)
    })
    if ($matchingPackage.Count -eq 1) { break }
    if ($matchingPackage.Count -gt 1) { throw "Store draft contains multiple copies of the uploaded package '$ExpectedPackageFileName'." }
    if ([DateTimeOffset]::UtcNow -ge $deadline) { throw "Store draft did not expose the uploaded package '$ExpectedPackageFileName' within $TimeoutSeconds seconds." }
    Start-Sleep -Seconds $PollIntervalSeconds
} while ($true)

$expectedVersion = [Version]$ExpectedPackageVersion
$unexpectedCurrentOrNewer = @($packages | Where-Object {
    if ($_ -isnot [System.Collections.IDictionary]) { return $false }
    if ($_ -eq $matchingPackage[0]) { return $false }
    $text = Get-PackageVersion -Package $_
    $parsed = $null
    return -not [string]::IsNullOrWhiteSpace($text) -and [Version]::TryParse($text, [ref]$parsed) -and $parsed -ge $expectedVersion
})
if ($unexpectedCurrentOrNewer.Count -gt 0) {
    $unexpectedNames = @($unexpectedCurrentOrNewer | ForEach-Object { Get-Text (Get-OptionalValue -Dictionary $_ -Name 'FileName') })
    throw "Store draft contains an unexpected package at the target version or newer: $($unexpectedNames -join ', ')."
}

$architectures = @($matchingPackage | ForEach-Object { Get-Text (Get-OptionalValue -Dictionary $_ -Name 'Architecture') } | Where-Object { $_ })
if ($architectures.Count -gt 0 -and @($architectures | Where-Object { $_ -ine 'X64' }).Count -gt 0) { throw "Store draft package architecture is not x64: $($architectures -join ', ')." }
foreach ($package in $matchingPackage) {
    foreach ($field in @('Status','FileStatus','PackageStatus')) {
        $value = Get-Text (Get-OptionalValue -Dictionary $package -Name $field)
        if ($value -match '(?i)(fail|error|invalid|reject)') { throw "Store draft package reports $field='$value'." }
    }
}

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
Write-Output "submission_id=$ExpectedSubmissionId"
Write-Output 'submission_status=PendingCommit'
Write-Output "package_version=$ExpectedPackageVersion"
Write-Output "package_file=$ExpectedPackageFileName"
Write-Output "verified_package_names=$($packageNames -join ',')"
Write-Output "application_package_count=$($packages.Count)"
Write-Output 'release_notes_verified=zh-CN,ja-JP,en-US'
