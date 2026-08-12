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

    $localAppInstallerPath = Get-SafePayloadFilePath $payloadRoot $metadata.appInstallerFileName
    [xml]$appInstaller = Get-Content -Raw -LiteralPath $localAppInstallerPath -Encoding UTF8
    $appInstallerRoot = $appInstaller.SelectSingleNode("/*[local-name()='AppInstaller']")
    $bundleNode = $appInstaller.SelectSingleNode("/*[local-name()='AppInstaller']/*[local-name()='MainBundle']")
    if ($null -eq $appInstallerRoot -or $null -eq $bundleNode) { throw 'The local App Installer file is missing AppInstaller/MainBundle metadata.' }
    if ($appInstallerRoot.Version -ne $metadata.packageVersion -or $appInstallerRoot.Uri -ne $metadata.appInstallerUri) { throw 'The local App Installer version or stable URI does not match the package metadata.' }
    if ($bundleNode.Name -ne $metadata.packageIdentityName -or $bundleNode.Publisher -ne $metadata.publisher -or $bundleNode.Version -ne $metadata.packageVersion) { throw 'The local App Installer package identity does not match the package metadata.' }
    if ([IO.Path]::GetFileName(([Uri]$bundleNode.Uri).AbsolutePath) -ne $metadata.bundleFileName) { throw 'The local App Installer bundle filename does not match the packaged bundle.' }
    Log "Validated local App Installer: $localAppInstallerPath; stable URI=$($metadata.appInstallerUri)."
    try {
        Start-Process -FilePath $localAppInstallerPath -PassThru -ErrorAction Stop | Out-Null
    } catch {
        throw "Windows App Installer could not open '$localAppInstallerPath'. Please install Microsoft App Installer or double-click this file manually. $($_.Exception.Message)"
    }
    Log 'Windows App Installer was opened through the .appinstaller file association. User confirmation is still required.'
    Write-Output 'Windows App Installer opened successfully. Please confirm installation in the App Installer window.'
    exit 0
} catch { Log "Installation bootstrap failed: $($_.Exception.Message)"; Write-Error $_; exit 1 } finally { Write-Output "INSTALL_LOG_PATH=$logPath" }
