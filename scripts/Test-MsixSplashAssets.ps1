[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$MsixPath,
    [string]$SourceAssetsDirectory = (Join-Path $PSScriptRoot '..\Assets')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.Drawing

if (-not (Test-Path -LiteralPath $MsixPath -PathType Leaf)) { throw "MSIX not found: $MsixPath" }
$expected = 100, 125, 150, 200, 400 | ForEach-Object { "Assets/SplashScreen.scale-$_.png" }
$outerArchive = [System.IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $MsixPath))
$archives = @()
try {
    if ([IO.Path]::GetExtension($MsixPath) -eq '.msixbundle') {
        foreach ($entry in $outerArchive.Entries | Where-Object { $_.FullName -like '*.msix' }) {
            $copy = [IO.MemoryStream]::new(); $input = $entry.Open()
            try { $input.CopyTo($copy); $copy.Position = 0; $archives += [IO.Compression.ZipArchive]::new($copy, [IO.Compression.ZipArchiveMode]::Read, $false) } finally { $input.Dispose() }
        }
    } else { $archives += $outerArchive }
    $mainArchive = $archives | Where-Object { $null -ne $_.GetEntry('UrbanPlanToolbox.exe') } | Select-Object -First 1
    $manifestEntry = $mainArchive.GetEntry('AppxManifest.xml')
    if ($null -eq $manifestEntry) { throw 'AppxManifest.xml is missing from the MSIX.' }
    $reader = [System.IO.StreamReader]::new($manifestEntry.Open())
    try { [xml]$manifest = $reader.ReadToEnd() } finally { $reader.Dispose() }
    $splash = Select-Xml -Xml $manifest -XPath "//*[local-name()='SplashScreen']" | Select-Object -First 1
    if ($null -eq $splash) { throw 'The MSIX manifest has no uap:SplashScreen element.' }
    $image = [string]$splash.Node.Image
    $background = [string]$splash.Node.BackgroundColor
    if ($image -ne 'Assets\SplashScreen.png' -or $background -notmatch '^#[0-9A-Fa-f]{6}([0-9A-Fa-f]{2})?$') { throw "Unexpected splash declaration: Image='$image', BackgroundColor='$background'." }

    $results = foreach ($entryName in $expected) {
        $entry = $archives | ForEach-Object { $_.GetEntry($entryName) } | Where-Object { $null -ne $_ } | Select-Object -First 1
        if ($null -eq $entry -or $entry.Length -eq 0) { throw "Missing or empty packaged splash asset: $entryName" }
        $stream = $entry.Open()
        try {
            $bytes = [System.IO.MemoryStream]::new()
            try { $stream.CopyTo($bytes); $assetBytes = $bytes.ToArray() } finally { $bytes.Dispose() }
            $imageStream = [System.IO.MemoryStream]::new($assetBytes)
            $bitmap = [System.Drawing.Bitmap]::new($imageStream)
            try {
                $visible = $false
                for ($x = 0; $x -lt $bitmap.Width -and -not $visible; $x += [Math]::Max(1, [int]($bitmap.Width / 32))) {
                    for ($y = 0; $y -lt $bitmap.Height; $y += [Math]::Max(1, [int]($bitmap.Height / 32))) {
                        if ($bitmap.GetPixel($x, $y).A -gt 0) { $visible = $true; break }
                    }
                }
                if (-not $visible) { throw "Packaged splash asset is fully transparent: $entryName" }
                $source = Join-Path $SourceAssetsDirectory (Split-Path $entryName -Leaf)
                $sha256 = [System.Security.Cryptography.SHA256]::Create()
                try { $packagedHash = ([BitConverter]::ToString($sha256.ComputeHash($assetBytes))).Replace('-', '').ToLowerInvariant() } finally { $sha256.Dispose() }
                $sourceExists = Test-Path -LiteralPath $source
                $sourceMatches = -not $sourceExists -or $packagedHash -eq (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant()
                if (-not $sourceMatches) { throw "Packaged splash asset does not match the source asset: $entryName" }
                [pscustomobject]@{ Asset = $entryName; Bytes = $entry.Length; Width = $bitmap.Width; Height = $bitmap.Height; Visible = $visible; SourceExists = $sourceExists; SourceMatches = $sourceMatches; Sha256 = $packagedHash }
            } finally { $bitmap.Dispose(); $imageStream.Dispose() }
        } finally { $stream.Dispose() }
    }
    $results
} finally { foreach ($archive in $archives) { $archive.Dispose() }; $outerArchive.Dispose() }
