[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PackagePath,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$ExpectedIdentityName,
    [string[]]$ExpectedLanguages = @('zh-CN', 'ja-JP', 'en-US'),
    [string]$OutputDirectory,
    [switch]$RequireBundle,
    [string]$MakePriPath
)

$ErrorActionPreference = 'Stop'

function Resolve-MakePriPath {
    param([string]$ExplicitPath)

    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath)) {
        if (-not (Test-Path -LiteralPath $ExplicitPath -PathType Leaf)) { throw "MakePri.exe was not found: $ExplicitPath" }
        return (Resolve-Path -LiteralPath $ExplicitPath).Path
    }

    $sdkBinRoot = 'C:\Program Files (x86)\Windows Kits\10\bin'
    if (-not (Test-Path -LiteralPath $sdkBinRoot -PathType Container)) { throw "Windows SDK bin directory was not found: $sdkBinRoot" }

    $candidates = foreach ($directory in Get-ChildItem -LiteralPath $sdkBinRoot -Directory) {
        $version = $null
        if (-not [Version]::TryParse($directory.Name, [ref]$version)) { continue }
        $candidate = Join-Path $directory.FullName 'x64\makepri.exe'
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            [pscustomobject]@{ Version = $version; Path = $candidate }
        }
    }

    $selected = @($candidates | Sort-Object Version -Descending | Select-Object -First 1)
    if ($selected.Count -ne 1) { throw "No x64 MakePri.exe was found under $sdkBinRoot." }
    return $selected[0].Path
}

$package = (Resolve-Path -LiteralPath $PackagePath).Path
if ([IO.Path]::GetExtension($package) -notin '.msix', '.msixbundle', '.msixupload') { throw 'PackagePath must point to an .msix, .msixbundle, or .msixupload file.' }
$MakePriPath = Resolve-MakePriPath -ExplicitPath $MakePriPath
$expected = @($ExpectedLanguages | ForEach-Object { $_.ToUpperInvariant() })
$temporaryDirectory = Join-Path ([IO.Path]::GetTempPath()) ('UrbanPlanToolbox-pri-' + [Guid]::NewGuid().ToString('N'))
$dumpPath = $null
try {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Directory]::CreateDirectory($temporaryDirectory) | Out-Null
    $msixPath = $package
    if ([IO.Path]::GetExtension($package) -eq '.msixupload') {
        $uploadDirectory = Join-Path $temporaryDirectory 'upload'
        [IO.Compression.ZipFile]::ExtractToDirectory($package, $uploadDirectory)
        $bundles = @(Get-ChildItem -LiteralPath $uploadDirectory -Filter '*.msixbundle' -File)
        $innerPackages = @(Get-ChildItem -LiteralPath $uploadDirectory -Filter '*.msix' -File)
        if ($bundles.Count -eq 1 -and $innerPackages.Count -eq 0) { $msixPath = $bundles[0].FullName }
        elseif ($bundles.Count -eq 0 -and $innerPackages.Count -eq 1 -and -not $RequireBundle) { $msixPath = $innerPackages[0].FullName }
        elseif ($RequireBundle) { throw 'Store update must remain an MSIX Bundle because the previously published Store version is a Bundle.' }
        else { throw 'The .msixupload must contain exactly one package.' }
    }
    if ([IO.Path]::GetExtension($msixPath) -eq '.msixbundle') {
        $bundleDirectory = Join-Path $temporaryDirectory 'bundle'
        [IO.Compression.ZipFile]::ExtractToDirectory($msixPath, $bundleDirectory)
        [xml]$bundleManifest = Get-Content -LiteralPath (Join-Path $bundleDirectory 'AppxMetadata\AppxBundleManifest.xml') -Raw
        $bundlePackages = @($bundleManifest.SelectNodes("//*[local-name()='Package']"))
        $main = @($bundlePackages | Where-Object { $_.GetAttribute('Type') -eq 'application' -and $_.GetAttribute('Architecture') -eq 'x64' })
        $resources = @($bundlePackages | Where-Object { $_.GetAttribute('Type') -eq 'resource' })
        if ($main.Count -ne 1) { throw 'Bundle must contain exactly one x64 application package.' }
        # Current Windows SDK bundles language and scale resources inside the application
        # package; older SDKs emitted separate resource packages. Both layouts are valid.
        $scales = if ($resources.Count -gt 0) { @($resources | ForEach-Object { $_.GetAttribute('ResourceId') -replace '^split\.scale-', '' -replace '^scale-', '' }) } else { @($main[0].SelectNodes("./*[local-name()='Resources']/*[local-name()='Resource']") | ForEach-Object { $_.GetAttribute('Scale') } | Where-Object { $_ }) }
        if ($resources.Count -gt 0) { foreach ($required in '100','125','150','400') { if ($scales -notcontains $required) { throw "Bundle is missing required scale-$required resource package." } } }
        $msixPath = Join-Path $bundleDirectory $main[0].GetAttribute('FileName')
        $resourceScales = $scales
    } else { $resourceScales = @() }
    $msixDirectory = Join-Path $temporaryDirectory 'msix'
    [IO.Compression.ZipFile]::ExtractToDirectory($msixPath, $msixDirectory)
    $manifest = [xml]::new()
    $manifest.Load((Join-Path $msixDirectory 'AppxManifest.xml'))
    $identityNode = $manifest.SelectSingleNode("/*[local-name()='Package']/*[local-name()='Identity']")
    $manifestIdentity = $identityNode.GetAttribute('Name')
    if ($manifestIdentity -ne $ExpectedIdentityName) { throw "Manifest identity mismatch. Expected=$ExpectedIdentityName Actual=$manifestIdentity" }
    $manifestLanguages = @($manifest.SelectNodes("/*[local-name()='Package']/*[local-name()='Resources']/*[local-name()='Resource']") | ForEach-Object { $_.GetAttribute('Language').ToUpperInvariant() } | Where-Object { $_ })
    if (@($expected | Where-Object { $_ -notin $manifestLanguages }).Count -gt 0 -or $manifestLanguages.Count -ne $expected.Count) { throw "Manifest language candidates are incomplete. Actual=$($manifestLanguages -join ', ')" }
    $priPath = Join-Path $msixDirectory 'resources.pri'
    if (-not (Test-Path -LiteralPath $priPath -PathType Leaf)) { throw 'MSIX is missing resources.pri.' }
    $dumpPath = if ($OutputDirectory) {
        New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
        Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) 'resources.pri.xml'
    } else { Join-Path $temporaryDirectory 'resources.pri.xml' }
    & $MakePriPath dump /if $priPath /of $dumpPath /o | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "MakePri dump failed with exit code $LASTEXITCODE." }
    $pri = [xml]::new()
    $pri.Load($dumpPath)
    $primaryMaps = @($pri.SelectNodes("/*[local-name()='PriInfo']/*[local-name()='ResourceMap'][@primary='true']"))
    if ($primaryMaps.Count -ne 1) { throw "Expected exactly one primary PRI ResourceMap; found $($primaryMaps.Count)." }
    $priResourceMapName = $primaryMaps[0].name
    if ($priResourceMapName -ne $ExpectedIdentityName) { throw "PRI primary ResourceMap mismatch. Expected=$ExpectedIdentityName Actual=$priResourceMapName" }
    $resources = @('AppDisplayName', 'AppDescription')
    foreach ($resourceName in $resources) {
        $node = $pri.SelectSingleNode("//*[local-name()='NamedResource'][@name='$resourceName']")
        if ($null -eq $node -or $node.GetAttribute('uri') -ne "ms-resource://$ExpectedIdentityName/Resources/$resourceName") { throw "PRI resource URI did not resolve for $resourceName." }
        $candidates = @($node.SelectNodes("./*[local-name()='Candidate']") | ForEach-Object { $_.GetAttribute('qualifiers').ToUpperInvariant() })
        foreach ($language in $expected) { if ($candidates -notcontains "LANGUAGE-$language") { throw "PRI resource $resourceName is missing Language-$language." } }
    }
    [pscustomobject]@{
        PackagePath = $package
        MsixPath = $msixPath
        ManifestIdentity = $manifestIdentity
        PriResourceMapName = $priResourceMapName
        ManifestLanguages = $manifestLanguages
        ResourceScales = $resourceScales
        ValidationResult = 'Passed'
        DumpPath = $dumpPath
        MakePriPath = $MakePriPath
    }
}
finally {
    if (Test-Path -LiteralPath $temporaryDirectory) { Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force }
}
