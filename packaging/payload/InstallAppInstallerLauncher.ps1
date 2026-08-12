[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$EntryLogPath,
    [Parameter(Mandatory)][string]$EntryCommandPath,
    [Parameter(Mandatory)][string]$EntryWorkingDirectory,
    [switch]$Elevated
)
$ErrorActionPreference = 'Stop'
$payloadScript = Join-Path $PSScriptRoot 'Install.ps1'
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $EntryLogPath) | Out-Null
"{0:u} [Install] Starting non-administrative App Installer bootstrap." -f (Get-Date) | Add-Content -LiteralPath $EntryLogPath -Encoding UTF8
if (-not (Test-Path -LiteralPath $payloadScript -PathType Leaf)) { throw "Missing payload script: $payloadScript" }
& powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File $payloadScript
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
exit 0
