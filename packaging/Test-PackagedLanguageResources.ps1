[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$MsixPath,
    [string]$MakePriPath = 'C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\makepri.exe',
    [string]$IntermediateDirectory
)

$ErrorActionPreference = 'Stop'
$expected = @('ZH-CN', 'JA-JP', 'EN-US')
$resolvedMsix = (Resolve-Path -LiteralPath $MsixPath).Path
if (-not (Test-Path -LiteralPath $MakePriPath -PathType Leaf)) { throw "MakePri.exe was not found: $MakePriPath" }

if ($IntermediateDirectory) {
    $resolvedIntermediateDirectory = (Resolve-Path -LiteralPath $IntermediateDirectory).Path
    $splitConfigPath = Join-Path $resolvedIntermediateDirectory 'split.priconfig.xml'
    if (-not (Test-Path -LiteralPath $splitConfigPath -PathType Leaf)) { throw "Generated split PRI configuration was not found: $splitConfigPath" }
    $splitConfig = Get-Content -LiteralPath $splitConfigPath -Raw
    $autoResourcePackageQualifiers = @([regex]::Matches($splitConfig, '<autoResourcePackage\s+qualifier="([^"]+)"') | ForEach-Object { $_.Groups[1].Value })
    if ($autoResourcePackageQualifiers -contains 'Language') { throw "Language must not be an auto resource package qualifier. Actual=$($autoResourcePackageQualifiers -join ', ')." }
    if ($autoResourcePackageQualifiers -notcontains 'Scale') { throw "Scale must remain an auto resource package qualifier. Actual=$($autoResourcePackageQualifiers -join ', ')." }
}
else {
    $resolvedIntermediateDirectory = $null
    $autoResourcePackageQualifiers = @()
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$temporaryDirectory = Join-Path ([IO.Path]::GetTempPath()) ("UrbanPlanToolbox-package-language-" + [Guid]::NewGuid().ToString('N'))
try {
    [IO.Directory]::CreateDirectory($temporaryDirectory) | Out-Null
    $archive = [IO.Compression.ZipFile]::OpenRead($resolvedMsix)
    try {
        $manifestEntry = $archive.GetEntry('AppxManifest.xml')
        $priEntry = $archive.GetEntry('resources.pri')
        if ($null -eq $manifestEntry -or $null -eq $priEntry) { throw 'MSIX is missing AppxManifest.xml or resources.pri.' }
        $manifestReader = [IO.StreamReader]::new($manifestEntry.Open())
        try { $manifestText = $manifestReader.ReadToEnd() } finally { $manifestReader.Dispose() }
        $languages = @([regex]::Matches($manifestText, '<Resource\s+Language="([^"]+)"') | ForEach-Object { $_.Groups[1].Value.ToUpperInvariant() })
        $missingManifest = @($expected | Where-Object { $_ -notin $languages })
        $unexpectedManifest = @($languages | Where-Object { $_ -notin $expected })
        if ($missingManifest.Count -gt 0 -or $unexpectedManifest.Count -gt 0 -or $languages.Count -ne $expected.Count -or $languages[0] -ne 'ZH-CN') {
            throw "MSIX manifest language validation failed. ManifestLanguages=$($languages -join ', '); MissingLanguages=$($missingManifest -join ', '); UnexpectedLanguages=$($unexpectedManifest -join ', '); DefaultLanguage=$($languages[0])."
        }
        $priPath = Join-Path $temporaryDirectory 'resources.pri'
        $entryStream = $priEntry.Open(); $fileStream = [IO.File]::Create($priPath)
        try { $entryStream.CopyTo($fileStream) } finally { $fileStream.Dispose(); $entryStream.Dispose() }
    }
    finally { $archive.Dispose() }

    $dumpPath = Join-Path $temporaryDirectory 'resources.pri.xml'
    & $MakePriPath dump /if $priPath /of $dumpPath /o | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "MakePri dump failed with exit code $LASTEXITCODE." }
    $dump = Get-Content -LiteralPath $dumpPath -Raw
    $priLanguageQualifierSet = @([regex]::Match($dump, '<Language>([^<]+)</Language>').Groups[1].Value.Split(',') | ForEach-Object { $_.Trim().ToUpperInvariant() } | Where-Object { $_ })
    $priLanguages = @($expected | Where-Object { $_ -in $priLanguageQualifierSet })
    $missingPri = @($expected | Where-Object { $_ -notin $priLanguages })
    $missingCandidates = @($expected | Where-Object { $dump -notmatch ('qualifiers="[^"]*Language-' + [regex]::Escape($_) + '(?:,|\")') })
    if ($missingPri.Count -gt 0 -or $priLanguages.Count -ne $expected.Count -or $missingCandidates.Count -gt 0) { throw "resources.pri language validation failed. PriLanguages=$($priLanguages -join ', '); MissingLanguages=$($missingPri -join ', '); MissingCandidates=$($missingCandidates -join ', ')." }

    $siblingPackages = Get-ChildItem -LiteralPath (Split-Path -Parent $resolvedMsix) -Filter '*.msix' -File
    $languageResourcePackages = @($siblingPackages | Where-Object Name -match '_language-[^.]+\.msix$' | Select-Object -ExpandProperty FullName)
    $scaleResourcePackages = @($siblingPackages | Where-Object Name -match '_scale-[^.]+\.msix$' | Select-Object -ExpandProperty FullName)
    if ($languageResourcePackages.Count -gt 0) { throw "The release directory contains language resource packages: $($languageResourcePackages -join ', ')." }

    [pscustomobject]@{
        MainPackagePath = $resolvedMsix
        ManifestLanguages = $languages
        PriLanguages = $priLanguages
        PriLanguageQualifierSet = $priLanguageQualifierSet
        AutoResourcePackageQualifiers = $autoResourcePackageQualifiers
        LanguageResourcePackages = $languageResourcePackages
        ScaleResourcePackages = $scaleResourcePackages
        MissingLanguages = @()
        UnexpectedLanguages = @()
        DefaultLanguage = $languages[0]
        ValidationResult = 'Passed'
    }
}
finally {
    if (Test-Path -LiteralPath $temporaryDirectory) { [IO.Directory]::Delete($temporaryDirectory, $true) }
}
