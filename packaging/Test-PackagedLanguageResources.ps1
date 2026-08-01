[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$MsixPath,
    [string]$MakePriPath = 'C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\makepri.exe'
)

$ErrorActionPreference = 'Stop'
$expected = @('ZH-CN', 'JA-JP', 'EN-US')
$resolvedMsix = (Resolve-Path -LiteralPath $MsixPath).Path
if (-not (Test-Path -LiteralPath $MakePriPath -PathType Leaf)) { throw "MakePri.exe was not found: $MakePriPath" }

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
        if ($missingManifest.Count -gt 0) { throw "MSIX manifest is missing language declarations: $($missingManifest -join ', ')" }
        $priPath = Join-Path $temporaryDirectory 'resources.pri'
        $entryStream = $priEntry.Open(); $fileStream = [IO.File]::Create($priPath)
        try { $entryStream.CopyTo($fileStream) } finally { $fileStream.Dispose(); $entryStream.Dispose() }
    }
    finally { $archive.Dispose() }

    $dumpPath = Join-Path $temporaryDirectory 'resources.pri.xml'
    & $MakePriPath dump /if $priPath /of $dumpPath /o | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "MakePri dump failed with exit code $LASTEXITCODE." }
    $dump = Get-Content -LiteralPath $dumpPath -Raw -Encoding Unicode
    $missingPri = @($expected | Where-Object { $dump -notmatch ("Language-" + [regex]::Escape($_)) })
    if ($missingPri.Count -gt 0) { throw "resources.pri is missing language candidates: $($missingPri -join ', ')" }
    [pscustomobject]@{ MsixPath=$resolvedMsix; ManifestLanguages=$languages; PriLanguages=$expected; Succeeded=$true }
}
finally {
    if (Test-Path -LiteralPath $temporaryDirectory) { [IO.Directory]::Delete($temporaryDirectory, $true) }
}
