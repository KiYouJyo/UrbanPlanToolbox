[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SignedMsixPath,
    [Parameter(Mandatory)][string]$PublicCertificatePath,
    [Parameter(Mandatory)][string]$WindowsAppRuntimeDependencyPath,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [Parameter(Mandatory)][string]$DisplayVersion,
    [Parameter(Mandatory)][string]$PackageVersion
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'payload\InstallerMetadata.ps1')
$names = Get-InstallerReleaseNames $DisplayVersion $PackageVersion
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$out = [IO.Path]::GetFullPath($OutputDirectory)
$repoPrefix = $repo.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
if ($out -eq $repo -or $out.StartsWith($repoPrefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Output must be outside the repository.' }
foreach ($path in @($SignedMsixPath, $PublicCertificatePath, $WindowsAppRuntimeDependencyPath)) { if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing input: $path" } }

$msix = Get-MsixPackageMetadata (Resolve-Path $SignedMsixPath)
if ($msix.Version -cne $PackageVersion -or $msix.Architecture -cne 'x64') { throw 'Input MSIX version or architecture mismatch.' }
$cert = [Security.Cryptography.X509Certificates.X509Certificate2]::new((Resolve-Path $PublicCertificatePath))
if ($cert.HasPrivateKey -or $cert.Subject -cne $msix.Publisher) { throw 'Invalid public certificate.' }

$root = Join-Path $out $names.ReleaseDirectoryName
if (Test-Path -LiteralPath $root) { throw 'Output already exists.' }
$payload = Join-Path $root 'payload'
$dependencyDirectory = Join-Path $payload 'Dependencies\x64'
New-Item -ItemType Directory -Path $dependencyDirectory -Force | Out-Null
$metadata = [ordered]@{ schemaVersion=1; displayVersion=$DisplayVersion; packageVersion=$PackageVersion; packageIdentityName=$msix.Name; publisher=$msix.Publisher; architecture='x64'; msixFileName=$names.MsixFileName; certificateFileName=$names.CertificateFileName }
$metadata | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $payload 'InstallerMetadata.json') -Encoding UTF8

$commandFiles = @(Get-ChildItem -LiteralPath $PSScriptRoot -File -Filter '*.cmd')
if ($commandFiles.Count -ne 2) { throw 'Expected exactly two root command files.' }
foreach ($commandFile in $commandFiles) { Copy-Item -LiteralPath $commandFile.FullName -Destination $root }
$readmeTemplate = @(Get-ChildItem -LiteralPath $PSScriptRoot -File -Filter '*.txt')
if ($readmeTemplate.Count -ne 1) { throw 'Expected exactly one root text template.' }
(Get-Content -LiteralPath $readmeTemplate[0].FullName -Raw -Encoding UTF8).Replace('{{DISPLAY_VERSION}}',$DisplayVersion).Replace('{{PACKAGE_VERSION}}',$PackageVersion) | Set-Content -LiteralPath (Join-Path $root $readmeTemplate[0].Name) -Encoding UTF8
foreach ($file in @('Install.ps1','Uninstall.ps1','InstallLauncher.ps1','UninstallLauncher.ps1','InstallerMetadata.ps1')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot "payload\$file") -Destination $payload }
Copy-Item -LiteralPath $SignedMsixPath -Destination (Join-Path $payload $metadata.msixFileName)
Copy-Item -LiteralPath $PublicCertificatePath -Destination (Join-Path $payload $metadata.certificateFileName)
Copy-Item -LiteralPath $WindowsAppRuntimeDependencyPath -Destination (Join-Path $dependencyDirectory 'Microsoft.WindowsAppRuntime.2.msix')
$hashLines = Get-ChildItem -LiteralPath $payload -Recurse -File | Where-Object { $_.Name -ne 'SHA256SUMS.txt' } | Sort-Object FullName | ForEach-Object { "$((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToUpperInvariant()) *$($_.FullName.Substring($payload.Length).TrimStart('\'))" }
Set-Content -LiteralPath (Join-Path $payload 'SHA256SUMS.txt') -Value $hashLines -Encoding UTF8
Write-Output $root
