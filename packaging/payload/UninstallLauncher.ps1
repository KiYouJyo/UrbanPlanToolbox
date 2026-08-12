[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$EntryLogPath,
    [Parameter(Mandatory)] [string]$EntryCommandPath,
    [Parameter(Mandatory)] [string]$EntryWorkingDirectory,
    [switch]$Elevated
)

$ErrorActionPreference = 'Stop'

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
    "{0:u} [Uninstall] {1}" -f (Get-Date), $Message | Add-Content -LiteralPath $EntryLogPath -Encoding UTF8
}

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Quote-Argument([string]$Value) {
    if ($Value.Contains('"')) { throw 'Windows paths cannot contain a quotation mark.' }
    return '"{0}"' -f $Value
}

try {
    $isAdministrator = Test-IsAdministrator
    $payloadScript = Join-Path $PSScriptRoot 'Uninstall.ps1'
    Write-EntryLog "CMD=$EntryCommandPath; WorkingDirectory=$EntryWorkingDirectory; Launcher=$PSCommandPath; Payload=$payloadScript; IsAdministrator=$isAdministrator; ElevatedArgument=$Elevated"
    if (-not (Test-Path -LiteralPath $payloadScript -PathType Leaf)) { throw "Missing payload script: $payloadScript" }

    if (-not $isAdministrator) {
        Write-EntryLog 'Requesting UAC elevation.'
        $argumentLine = @(
            '-NoLogo', '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File',
            (Quote-Argument $PSCommandPath), '-EntryLogPath', (Quote-Argument $EntryLogPath), '-EntryCommandPath', (Quote-Argument $EntryCommandPath), '-EntryWorkingDirectory', (Quote-Argument $EntryWorkingDirectory), '-Elevated'
        ) -join ' '
        try {
            $child = Start-Process -FilePath 'powershell.exe' -ArgumentList $argumentLine -Verb RunAs -Wait -PassThru
        }
        catch {
            Write-EntryLog "UAC request failed or was cancelled: $($_.Exception.Message)"
            Write-Error 'UAC elevation was cancelled or could not be started.'
            exit 1223
        }
        Write-EntryLog "UAC child exit code=$($child.ExitCode)"
        exit $child.ExitCode
    }

    Write-EntryLog 'Running Uninstall.ps1 in one UAC-elevated process with -RemoveCertificate.'
    & powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File $payloadScript -RemoveCertificate
    $payloadExitCode = $LASTEXITCODE
    Write-EntryLog "Uninstall.ps1 exit code=$payloadExitCode"
    exit $payloadExitCode
}
catch {
    Write-EntryLog "Launcher failure: $($_.Exception.Message)"
    Write-Error $_
    exit 1
}
