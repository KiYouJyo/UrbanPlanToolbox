[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PackagePath,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$ExpectedIdentityName,
    [string[]]$ExpectedLanguages = @('zh-CN', 'ja-JP', 'en-US'),
    [string]$OutputDirectory,
    [string]$MakePriPath = 'C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\makepri.exe'
)

$ErrorActionPreference = 'Stop'
$package = (Resolve-Path -LiteralPath $PackagePath).Path
if ([IO.Path]::GetExtension($package) -notin '.msix', '.msixupload') { throw 'PackagePath must point to an .msix or .msixupload file.' }
if (-not (Test-Path -LiteralPath $MakePriPath -PathType Leaf)) { throw "MakePri.exe was not found: $MakePriPath" }
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
        $innerPackages = @(Get-ChildItem -LiteralPath $uploadDirectory -Filter '*.msix' -File)
        if ($innerPackages.Count -ne 1) { throw "The .msixupload must contain exactly one .msix; found $($innerPackages.Count)." }
        $msixPath = $innerPackages[0].FullName
    }
    $msixDirectory = Join-Path $temporaryDirectory 'msix'
    [IO.Compression.ZipFile]::ExtractToDirectory($msixPath, $msixDirectory)
    $manifest = [xml]::new()
    $manifest.Load((Join-Path $msixDirectory 'AppxManifest.xml'))
    $identityNode = $manifest.SelectSingleNode("/*[local-name()='Package']/*[local-name()='Identity']")
    $manifestIdentity = $identityNode.GetAttribute('Name')
    if ($manifestIdentity -ne $ExpectedIdentityName) { throw "Manifest identity mismatch. Expected=$ExpectedIdentityName Actual=$manifestIdentity" }
    $manifestLanguages = @($manifest.SelectNodes("/*[local-name()='Package']/*[local-name()='Resources']/*[local-name()='Resource']") | ForEach-Object { $_.GetAttribute('Language').ToUpperInvariant() })
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
    [pscustomobject]@{ PackagePath = $package; MsixPath = $msixPath; ManifestIdentity = $manifestIdentity; PriResourceMapName = $priResourceMapName; ManifestLanguages = $manifestLanguages; ValidationResult = 'Passed'; DumpPath = $dumpPath }
}
finally {
    if (Test-Path -LiteralPath $temporaryDirectory) { Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force }
}
