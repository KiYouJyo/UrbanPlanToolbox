[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$settingsPath = Join-Path $repositoryRoot 'Models/AppSettings.cs'
$backupPath = Join-Path $repositoryRoot 'Services/BackupDataService.cs'
$credentialStorePath = Join-Path $repositoryRoot 'Services/WebDavCredentialStore.cs'
$privacyPath = Join-Path $repositoryRoot 'PRIVACY.md'

foreach ($path in @($settingsPath, $backupPath, $credentialStorePath, $privacyPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "WebDAV privacy boundary check is missing required file: $path"
    }
}

$settings = Get-Content -LiteralPath $settingsPath -Raw -Encoding utf8
$backup = Get-Content -LiteralPath $backupPath -Raw -Encoding utf8
$credentialStore = Get-Content -LiteralPath $credentialStorePath -Raw -Encoding utf8
$privacy = Get-Content -LiteralPath $privacyPath -Raw -Encoding utf8

if ($settings -match '(?i)webdav') {
    throw 'WebDAV configuration must not be persisted in AppSettings/settings.json.'
}
if ($backup -match '(?i)webdav-profile|passwordvault') {
    throw 'Portable .uptbackup contract unexpectedly references WebDAV configuration or credential storage.'
}
if ($credentialStore -notmatch 'PasswordVault') {
    throw 'WebDAV credential storage is not backed by Windows Credential Locker.'
}
if ($privacy -notmatch '(?i)webdav') {
    throw 'Privacy policy does not document WebDAV behavior.'
}

Write-Host 'WebDAV privacy and credential boundary checks passed.'
