[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SourceCommit,
    [Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+\.\d+$')][string]$PackageVersion,
    [ValidateSet('Debug','Release')][string]$Configuration = 'Release',
    [ValidateSet('x64')][string]$Platform = 'x64',
    [Parameter(Mandatory)][string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$output = [IO.Path]::GetFullPath($OutputDirectory)
$repoPrefix = $repoRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
if ($output -eq $repoRoot -or $output.StartsWith($repoPrefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Store package output must be outside the repository.' }
if ($PackageVersion -ne '1.1.0.0') { throw 'This v1.1 workflow only accepts Store package version 1.1.0.0.' }
git -C $repoRoot rev-parse --verify "$SourceCommit^{commit}" | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Source commit does not exist: $SourceCommit" }

$manifestPath = Join-Path $repoRoot 'Package.Store.appxmanifest'
[xml]$manifest = [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($manifestPath))
$identity = $manifest.Package.Identity
if ($identity.Name -ne 'JoKiy.UrbanPlanToolbox' -or $identity.Publisher -ne 'CN=C4E4B33A-7B77-4121-897C-7D720A5471F8' -or $identity.Version -ne $PackageVersion) { throw 'Store manifest identity, publisher, or version is invalid.' }
if ($manifest.Package.Properties.PublisherDisplayName -cne ('Jo Kiy' + [char]333)) { throw 'Store publisher display name is invalid.' }

New-Item -ItemType Directory -Force -Path $output | Out-Null
$packageDirectory = Join-Path $output 'AppPackages'
& dotnet restore (Join-Path $repoRoot 'UrbanPlanToolbox.slnx') -p:Configuration=$Configuration -p:Platform=$Platform
if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
& dotnet build (Join-Path $repoRoot 'UrbanPlanToolbox.csproj') -c $Configuration -p:Platform=$Platform -p:DistributionChannel=Store -p:RuntimeIdentifier=win-x64 -p:GenerateAppxPackageOnBuild=true -p:AppxPackageSigningEnabled=false -p:AppxBundle=Never -p:UapAppxPackageBuildMode=StoreUpload -p:AppxPackageDir="$packageDirectory\" --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Store package build failed.' }

$upload = Get-ChildItem -LiteralPath $packageDirectory -Recurse -Filter '*.msixupload' | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
if ($null -eq $upload) { throw 'No .msixupload was produced.' }
if ($upload.Name -notmatch 'x64') { throw 'Store package is not x64.' }
$resources = @('Strings\zh-CN\Resources.resw','Strings\ja-JP\Resources.resw','Strings\en-US\Resources.resw')
foreach ($resource in $resources) { if (-not (Test-Path -LiteralPath (Join-Path $repoRoot $resource))) { throw "Missing packaged-language source: $resource" } }
$sensitive = Get-ChildItem -LiteralPath $packageDirectory -Recurse -File | Where-Object { $_.Extension -in '.pfx','.p12','.cer','.key' }
if ($sensitive) { throw "Sensitive file found in Store output: $($sensitive.FullName -join ', ')" }
$hash = (Get-FileHash -LiteralPath $upload.FullName -Algorithm SHA256).Hash.ToUpperInvariant()
[pscustomobject]@{ sourceCommit=$SourceCommit; packageVersion=$PackageVersion; package=$upload.FullName; sha256=$hash; channel='Store'; signed=$false } | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $output 'store-package-build.json') -Encoding UTF8
Write-Output "MSIXUPLOAD=$($upload.FullName)"
Write-Output "SHA256=$hash"
