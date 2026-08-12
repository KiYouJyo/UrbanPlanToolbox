[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$EntryLogPath,
    [Parameter(Mandatory)] [string]$EntryCommandPath,
    [Parameter(Mandatory)] [string]$EntryWorkingDirectory,
    [switch]$Elevated
)

$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8; $OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }

function Initialize-EntryLog {
    $directory = Split-Path -Parent $EntryLogPath
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    $utf8WithBom = [System.Text.UTF8Encoding]::new($true)
    if (-not (Test-Path -LiteralPath $EntryLogPath -PathType Leaf)) {
        [System.IO.File]::WriteAllText($EntryLogPath, '', $utf8WithBom)
        return
    }

    $bytes = [System.IO.File]::ReadAllBytes($EntryLogPath)
    $hasUtf8Bom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    if (-not $hasUtf8Bom) {
        try { $existing = [System.Text.UTF8Encoding]::new($false, $true).GetString($bytes) }
        catch { $existing = [System.Text.Encoding]::Default.GetString($bytes) }
        [System.IO.File]::WriteAllText($EntryLogPath, $existing, $utf8WithBom)
    }
}

function Write-EntryLog([string]$Message) {
    Initialize-EntryLog
    "{0:u} [Install] {1}" -f (Get-Date), $Message | Add-Content -LiteralPath $EntryLogPath -Encoding UTF8
}

try {
    $payloadScript = Join-Path $PSScriptRoot 'Install.ps1'
    Write-EntryLog "CMD=$EntryCommandPath; WorkingDirectory=$EntryWorkingDirectory; Launcher=$PSCommandPath; Payload=$payloadScript; ElevatedArgument=$Elevated"
    if (-not (Test-Path -LiteralPath $payloadScript -PathType Leaf)) { throw "Missing payload script: $payloadScript" }
    Write-EntryLog 'Running Install.ps1 in the current user context; only certificate trust setup may request UAC.'
    & powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File $payloadScript
    $payloadExitCode = $LASTEXITCODE
    Write-EntryLog "Install.ps1 exit code=$payloadExitCode"
    exit $payloadExitCode
}
catch {
    Write-EntryLog "Launcher failure: $($_.Exception.Message)"
    Write-Error $_
    exit 1
}
