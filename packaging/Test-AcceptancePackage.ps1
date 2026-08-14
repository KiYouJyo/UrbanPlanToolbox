[CmdletBinding()]
param([Parameter(Mandatory)][string]$PackagePath)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$release = [IO.File]::ReadAllText((Join-Path $root 'release/release.json'), [Text.UTF8Encoding]::new($false)) | ConvertFrom-Json
$item = Get-Item -LiteralPath $PackagePath
if ($item.Name -ne "UrbanPlanToolbox_$($release.product.packageVersion)_x64.msixbundle") { throw "Unexpected bundle filename: $($item.Name)" }
$signature = Get-AuthenticodeSignature -FilePath $item.FullName
if ($signature.Status -ne 'Valid') { throw "Signature is not valid: $($signature.Status)" }
if ($signature.SignerCertificate.Subject -cne 'CN=AppPublisher') { throw "Unexpected signer: $($signature.SignerCertificate.Subject)" }
if ($signature.SignerCertificate.Thumbprint -cne 'BD85AD77A651C86CA01A480C8E9BC64952993F98') { throw 'Unexpected signer thumbprint.' }

& "$PSScriptRoot/Test-PackageResourceIdentity.ps1" -PackagePath $item.FullName -ExpectedIdentityName '556F80C5-C4D4-452B-93B4-00DE3FA7AC29' -RequireBundle
if ($LASTEXITCODE -ne 0) { throw "PRI validation failed: $LASTEXITCODE" }

$temp = Join-Path ([IO.Path]::GetTempPath()) ('upt-accept-' + [guid]::NewGuid().ToString('N'))
try {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::ExtractToDirectory($item.FullName, $temp)
    [xml]$bundleManifest = Get-Content -LiteralPath (Join-Path $temp 'AppxMetadata\AppxBundleManifest.xml') -Raw
    $mainPackages = @($bundleManifest.SelectNodes("//*[local-name()='Package']") | Where-Object { $_.GetAttribute('Type') -eq 'application' -and $_.GetAttribute('Architecture') -eq 'x64' })
    if ($mainPackages.Count -ne 1) { throw 'Bundle must contain exactly one x64 application package.' }
    $msix = Join-Path $temp $mainPackages[0].GetAttribute('FileName')
    if (-not (Test-Path -LiteralPath $msix -PathType Leaf)) { throw 'Bundle main application package is missing.' }
    $main = Join-Path $temp 'main'
    [IO.Compression.ZipFile]::ExtractToDirectory($msix, $main)
    $notes = Join-Path $main "Assets\Data\ReleaseNotes\$($release.product.version).json"
    if (-not (Test-Path -LiteralPath $notes -PathType Leaf)) { throw 'Bundled release notes are missing.' }
    Write-Host "PASS: Static acceptance $($item.Name) bytes=$($item.Length) sha256=$((Get-FileHash $item.FullName -Algorithm SHA256).Hash)"
}
finally {
    if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Recurse -Force }
}
