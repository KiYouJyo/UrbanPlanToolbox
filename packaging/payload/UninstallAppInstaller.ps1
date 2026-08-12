[CmdletBinding()]
param([switch]$RemoveCertificate)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'InstallerMetadata.ps1')
$metadata = Get-InstallerMetadata $PSScriptRoot
$packageName = '556F80C5-C4D4-452B-93B4-00DE3FA7AC29'
$publisher = 'CN=AppPublisher'
if ($metadata.packageIdentityName -cne $packageName -or $metadata.publisher -cne $publisher) { throw 'Installer metadata does not describe the GitHub package identity.' }
$certificatePath = Get-SafePayloadFilePath $PSScriptRoot $metadata.certificateFileName
if (-not (Test-Path -LiteralPath $certificatePath -PathType Leaf)) { throw 'CER is missing; cannot verify the exact certificate thumbprint.' }
$certificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($certificatePath)
if ($certificate.HasPrivateKey -or $certificate.Subject -cne $publisher) { throw 'CER does not match the GitHub publisher or contains a private key.' }
$thumbprint = $certificate.Thumbprint.ToUpperInvariant()
$package = Get-AppxPackage -Name $packageName -ErrorAction SilentlyContinue | Where-Object Publisher -ceq $publisher | Sort-Object Version -Descending | Select-Object -First 1
if (-not $package) { Write-Output 'UrbanPlanToolbox is not installed.'; exit 0 }
Remove-AppxPackage -Package $package.PackageFullName -ErrorAction Stop
$remaining = @(Get-AppxPackage -Name $packageName -ErrorAction SilentlyContinue | Where-Object Publisher -ceq $publisher)
if ($remaining.Count -ne 0) { throw "GitHub package is still installed: $($remaining[0].PackageFullName)" }
Write-Output "Removed $($package.PackageFullName)."
if ($RemoveCertificate) {
    $certificateStorePath = "Cert:\LocalMachine\TrustedPeople\$thumbprint"
    if (Test-Path -LiteralPath $certificateStorePath) { Remove-Item -LiteralPath $certificateStorePath -ErrorAction Stop; Write-Output "Removed certificate $thumbprint." }
    else { Write-Output "Certificate $thumbprint was not present in LocalMachine TrustedPeople." }
}
