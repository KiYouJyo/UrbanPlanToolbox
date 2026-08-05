[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SourceCommit,
    [Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+\.\d+$')][string]$PackageVersion,
    [ValidateSet('Debug','Release')][string]$Configuration = 'Release',
    [ValidateSet('x64')][string]$Platform = 'x64',
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$MsBuildPath
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$output = [IO.Path]::GetFullPath($OutputDirectory)
$repoPrefix = $repoRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
if ($output -eq $repoRoot -or $output.StartsWith($repoPrefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Store package output must be outside the repository.' }
if (Test-Path -LiteralPath $output) {
    if (@(Get-ChildItem -LiteralPath $output -Force).Count -gt 0) { throw "Store package output directory must be new or empty: $output" }
}

$sourceCommitResolved = (& git -C $repoRoot rev-parse --verify "$SourceCommit^{commit}").Trim()
if ($LASTEXITCODE -ne 0) { throw "Source commit does not exist: $SourceCommit" }
$headCommit = (& git -C $repoRoot rev-parse HEAD).Trim()
if ($headCommit -ne $sourceCommitResolved) { throw "SourceCommit must be the current HEAD. HEAD=$headCommit SourceCommit=$sourceCommitResolved" }
$workingTreeState = & git -C $repoRoot status --porcelain
if ($LASTEXITCODE -ne 0 -or $workingTreeState) { throw 'The source working tree must be clean before a Store package build.' }

$manifestPath = Join-Path $repoRoot 'Package.Store.appxmanifest'
[xml]$manifest = [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($manifestPath))
$identity = $manifest.Package.Identity
if ($identity.Name -ne 'JoKiy.UrbanPlanToolbox' -or $identity.Publisher -ne 'CN=C4E4B33A-7B77-4121-897C-7D720A5471F8' -or $identity.Version -ne $PackageVersion) { throw 'Store manifest identity, publisher, or version is invalid.' }
if ($manifest.Package.Properties.PublisherDisplayName -cne ('Jo Kiy' + [char]333)) { throw 'Store publisher display name is invalid.' }

$projectPath = Join-Path $repoRoot 'UrbanPlanToolbox.csproj'
[xml]$project = [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($projectPath))
$projectVersion = @($project.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ })[0]
if (-not $projectVersion -or $projectVersion -notmatch '^\d+\.\d+\.\d+$') { throw 'UrbanPlanToolbox.csproj must declare a three-part Version.' }
$expectedPackageVersion = "$projectVersion.0"
if ($PackageVersion -ne $expectedPackageVersion) { throw "Store package version must match the project version. Expected=$expectedPackageVersion Actual=$PackageVersion" }

if (-not $MsBuildPath) {
    $vswhere = 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe'
    if (-not (Test-Path -LiteralPath $vswhere -PathType Leaf)) { throw "vswhere.exe was not found: $vswhere" }
    $vsInstall = (& $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -property installationPath).Trim()
    $MsBuildPath = Join-Path $vsInstall 'MSBuild\Current\Bin\amd64\MSBuild.exe'
}
if (-not (Test-Path -LiteralPath $MsBuildPath -PathType Leaf)) { throw "MSBuild.exe was not found: $MsBuildPath" }

New-Item -ItemType Directory -Force -Path $output | Out-Null
$temporaryWorktree = Join-Path ([IO.Path]::GetTempPath()) ('UrbanPlanToolbox-store-' + [Guid]::NewGuid().ToString('N'))
$worktreeAdded = $false
try {
    # A disposable worktree prevents a previous GitHub-channel build from reusing obj\ PRI inputs.
    & git -C $repoRoot worktree add --detach $temporaryWorktree $sourceCommitResolved
    if ($LASTEXITCODE -ne 0) { throw 'Unable to create the isolated Store build worktree.' }
    $worktreeAdded = $true

    $packageDirectory = Join-Path $output 'AppPackages'
    & dotnet restore (Join-Path $temporaryWorktree 'UrbanPlanToolbox.slnx') "-p:Configuration=$Configuration" "-p:Platform=$Platform"
    if ($LASTEXITCODE -ne 0) { throw 'Restore in the isolated Store build worktree failed.' }
    & $MsBuildPath (Join-Path $temporaryWorktree 'UrbanPlanToolbox.csproj') /t:Build /m "/p:Configuration=$Configuration" "/p:Platform=$Platform" /p:DistributionChannel=Store /p:RuntimeIdentifier=win-x64 /p:GenerateAppxPackageOnBuild=true /p:AppxPackageSigningEnabled=false /p:AppxBundle=Always /p:AppxBundlePlatforms=x64 /p:UapAppxPackageBuildMode=StoreUpload "/p:AppxPackageDir=$packageDirectory\\" /p:Restore=false
    if ($LASTEXITCODE -ne 0) { throw 'Store package build failed.' }

    $upload = @(Get-ChildItem -LiteralPath $packageDirectory -Recurse -Filter '*.msixupload' -File)
    if ($upload.Count -ne 1) { throw "Expected exactly one .msixupload; found $($upload.Count)." }
    if ($upload[0].Name -notmatch 'bundle') { throw 'Store update must remain an MSIX Bundle because the previously published Store version is a Bundle.' }
    $identityValidation = & (Join-Path $temporaryWorktree 'packaging\Test-PackageResourceIdentity.ps1') -PackagePath $upload[0].FullName -ExpectedIdentityName $identity.Name -RequireBundle -OutputDirectory (Join-Path $output 'pri-validation')
    if ($LASTEXITCODE -ne 0) { throw 'Store package PRI identity validation failed.' }

    $sensitive = Get-ChildItem -LiteralPath $packageDirectory -Recurse -File | Where-Object { $_.Extension -in '.pfx','.p12','.cer','.key' }
    if ($sensitive) { throw "Sensitive file found in Store output: $($sensitive.FullName -join ', ')" }
    $hash = (Get-FileHash -LiteralPath $upload[0].FullName -Algorithm SHA256).Hash.ToUpperInvariant()
    [pscustomobject]@{
        sourceCommit = $sourceCommitResolved; packageVersion = $PackageVersion; package = $upload[0].FullName; sha256 = $hash
        channel = 'Store'; signed = $false; manifestIdentity = $identity.Name; priResourceMapName = $identityValidation.PriResourceMapName
        languages = @($identityValidation.ManifestLanguages); validationResult = $identityValidation.ValidationResult
        wackReady = $true; buildUtc = [DateTime]::UtcNow.ToString('O')
    } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $output 'store-package-build.json') -Encoding UTF8
    Write-Output "MSIXUPLOAD=$($upload[0].FullName)"
    Write-Output 'STORE_UPLOAD_FORMAT=MSIXBUNDLE'
    Write-Output "STORE_BUNDLE_VERSION=$PackageVersion"
    Write-Output "STORE_MAIN_ARCHITECTURE=x64"
    Write-Output "STORE_RESOURCE_SCALES=$($identityValidation.ResourceScales -join ',')"
    Write-Output "STORE_MANIFEST_IDENTITY=$($identityValidation.ManifestIdentity)"
    Write-Output "STORE_PRI_RESOURCE_MAP=$($identityValidation.PriResourceMapName)"
    Write-Output "SHA256=$hash"
}
finally {
    if ($worktreeAdded) { & git -C $repoRoot worktree remove --force $temporaryWorktree | Out-Null }
    elseif (Test-Path -LiteralPath $temporaryWorktree) { Remove-Item -LiteralPath $temporaryWorktree -Recurse -Force }
}
