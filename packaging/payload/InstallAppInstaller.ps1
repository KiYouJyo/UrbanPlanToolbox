[CmdletBinding()]
param([switch]$LaunchAfterInstall, [switch]$ImportCertificateOnly)
$ErrorActionPreference = 'Stop'
$payloadRoot = $PSScriptRoot
. (Join-Path $payloadRoot 'InstallerMetadata.ps1')
$logDirectory = Join-Path $env:LOCALAPPDATA 'UrbanPlanToolbox\Logs'
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
$logPath = Join-Path $logDirectory ("Install-{0:yyyyMMdd-HHmmss}.log" -f (Get-Date))
function Log([string]$Message) { "{0:u} {1}" -f (Get-Date), $Message | Tee-Object -FilePath $logPath -Append }
function Is-Administrator { $p = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent()); $p.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator) }
try {
    $metadata = Get-InstallerMetadata $payloadRoot
    $hashMap = @{}
    Get-Content -LiteralPath (Join-Path $payloadRoot 'SHA256SUMS.txt') | ForEach-Object { if ($_ -match '^(?<hash>[A-Fa-f0-9]{64}) \*(?<name>.+)$') { $hashMap[$matches.name.Replace('/','\')] = $matches.hash.ToUpperInvariant() } }
    foreach ($name in @($metadata.bundleFileName, $metadata.certificateFileName, $metadata.appInstallerFileName, 'SHA256SUMS.txt')) {
        $path = Get-SafePayloadFilePath $payloadRoot $name
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing payload file: $name" }
        if ($name -ne 'SHA256SUMS.txt' -and $hashMap[$name] -ne (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToUpperInvariant()) { throw "SHA-256 mismatch: $name" }
    }
    $certPath = Get-SafePayloadFilePath $payloadRoot $metadata.certificateFileName
    $certificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new($certPath)
    if ($certificate.HasPrivateKey -or $certificate.Subject -cne $metadata.publisher) { throw 'Certificate publisher mismatch.' }
    $thumbprint = $certificate.Thumbprint.ToUpperInvariant()
    Log "Validated $($metadata.displayVersion), Publisher=$($metadata.publisher), Thumbprint=$thumbprint."
    $trusted = Get-ChildItem "Cert:\LocalMachine\TrustedPeople" -ErrorAction SilentlyContinue | Where-Object Thumbprint -eq $thumbprint
    if (-not $trusted -and $ImportCertificateOnly) {
        if (-not (Is-Administrator)) { throw 'Certificate trust requires elevation.' }
        Import-Certificate -FilePath $certPath -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople' | Out-Null
        Log "Imported the existing public certificate into LocalMachine TrustedPeople."
        exit 0
    }
    if (-not $trusted -and -not (Is-Administrator)) {
        $arguments = @('-NoLogo','-NoProfile','-ExecutionPolicy','Bypass','-File',"`"$PSCommandPath`"",'-ImportCertificateOnly') -join ' '
        $elevated = Start-Process -FilePath 'powershell.exe' -ArgumentList $arguments -Verb RunAs -Wait -PassThru
        if ($elevated.ExitCode -ne 0) { throw 'Certificate trust setup was cancelled or failed.' }
        Log 'Certificate trust setup completed through a UAC-elevated helper.'
    } elseif (-not $trusted) {
        Import-Certificate -FilePath $certPath -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople' | Out-Null
        Log "Imported the existing public certificate into LocalMachine TrustedPeople."
    } else { Log 'The matching public certificate is already trusted; no duplicate import performed.' }
    $uri = [Uri]::EscapeDataString($metadata.appInstallerUri)
    Log "Launching Windows App Installer through the stable URI: $($metadata.appInstallerUri)"
    $process = Start-Process -FilePath 'explorer.exe' -ArgumentList "ms-appinstaller:?source=$uri" -PassThru
    if (-not $process) { throw 'Windows App Installer could not be started.' }
    Log 'Windows App Installer was started; it owns package installation and association creation.'
    exit 0
} catch { Log "Installation bootstrap failed: $($_.Exception.Message)"; Write-Error $_; exit 1 } finally { Write-Output "INSTALL_LOG_PATH=$logPath" }
