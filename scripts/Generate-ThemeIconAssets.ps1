[CmdletBinding()]
param([string]$AssetsDirectory = (Join-Path $PSScriptRoot '..\Assets'))

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$assets = (Resolve-Path -LiteralPath $AssetsDirectory).Path
$targetSizes = 16, 20, 24, 30, 32, 36, 40, 44, 48, 60, 64, 72, 80, 96, 256

function Set-WhiteGlyphFromBlackSource {
    param([Parameter(Mandatory)][string]$Source, [Parameter(Mandatory)][string]$Destination)
    $bitmap = [System.Drawing.Bitmap]::new($Source)
    $temporary = "$Destination.$([Guid]::NewGuid().ToString('N')).tmp"
    try {
        for ($y = 0; $y -lt $bitmap.Height; $y++) {
            for ($x = 0; $x -lt $bitmap.Width; $x++) {
                $pixel = $bitmap.GetPixel($x, $y)
                if ($pixel.A -gt 0) { $bitmap.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($pixel.A, 255, 255, 255)) }
            }
        }
        $bitmap.Save($temporary, [System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $bitmap.Dispose() }
    Move-Item -LiteralPath $temporary -Destination $Destination -Force
}

function New-PngIco {
    param([Parameter(Mandatory)][string[]]$PngPaths, [Parameter(Mandatory)][string]$Destination)
    $frames = foreach ($path in $PngPaths) {
        $bytes = [IO.File]::ReadAllBytes($path); $bitmap = [System.Drawing.Bitmap]::new($path)
        try { [pscustomobject]@{ Bytes = $bytes; Width = $bitmap.Width; Height = $bitmap.Height } } finally { $bitmap.Dispose() }
    }
    $stream = [IO.File]::Create($Destination); $writer = [IO.BinaryWriter]::new($stream)
    try {
        $writer.Write([UInt16]0); $writer.Write([UInt16]1); $writer.Write([UInt16]$frames.Count); $offset = 6 + (16 * $frames.Count)
        foreach ($frame in $frames) {
            $writer.Write([byte]($frame.Width % 256)); $writer.Write([byte]($frame.Height % 256)); $writer.Write([byte]0); $writer.Write([byte]0)
            $writer.Write([UInt16]1); $writer.Write([UInt16]32); $writer.Write([UInt32]$frame.Bytes.Length); $writer.Write([UInt32]$offset); $offset += $frame.Bytes.Length
        }
        foreach ($frame in $frames) { $writer.Write($frame.Bytes) }
    } finally { $writer.Dispose(); $stream.Dispose() }
}

foreach ($size in $targetSizes) {
    $forDarkShellTheme = Join-Path $assets "Square44x44Logo.targetsize-$size`_altform-unplated.png"
    $legacyLightUnplated = Join-Path $assets "Square44x44Logo.targetsize-$size`_altform-lightunplated.png"
    $forLightShellTheme = Join-Path $assets "Square44x44Logo.targetsize-$size`_altform-unplated_theme-light.png"
    if (-not (Test-Path -LiteralPath $forDarkShellTheme -PathType Leaf) -or -not (Test-Path -LiteralPath $legacyLightUnplated -PathType Leaf)) { throw "Missing target-size source: $size" }
    $blackSource = if (Test-Path -LiteralPath $forLightShellTheme -PathType Leaf) { $forLightShellTheme } else { $forDarkShellTheme }
    if ($blackSource -ne $forLightShellTheme) { Copy-Item -LiteralPath $blackSource -Destination $forLightShellTheme -Force }
    Set-WhiteGlyphFromBlackSource -Source $blackSource -Destination $legacyLightUnplated
    Set-WhiteGlyphFromBlackSource -Source $blackSource -Destination $forDarkShellTheme
}
foreach ($scale in 100, 125, 150, 200, 400) {
    $forDarkShellTheme = Join-Path $assets "Square44x44Logo.scale-$scale.png"; $forLightShellTheme = Join-Path $assets "Square44x44Logo.scale-$scale`_theme-light.png"
    if (-not (Test-Path -LiteralPath $forDarkShellTheme -PathType Leaf)) { throw "Missing scale source: $scale" }
    $blackSource = if (Test-Path -LiteralPath $forLightShellTheme -PathType Leaf) { $forLightShellTheme } else { $forDarkShellTheme }
    if ($blackSource -ne $forLightShellTheme) { Copy-Item -LiteralPath $blackSource -Destination $forLightShellTheme -Force }; Set-WhiteGlyphFromBlackSource -Source $blackSource -Destination $forDarkShellTheme
}
$square150ForDarkShellTheme = Join-Path $assets 'Square150x150Logo.scale-200.png'; $square150ForLightShellTheme = Join-Path $assets 'Square150x150Logo.scale-200_theme-light.png'
$square150BlackSource = if (Test-Path -LiteralPath $square150ForLightShellTheme -PathType Leaf) { $square150ForLightShellTheme } else { $square150ForDarkShellTheme }
if ($square150BlackSource -ne $square150ForLightShellTheme) { Copy-Item -LiteralPath $square150BlackSource -Destination $square150ForLightShellTheme -Force }; Set-WhiteGlyphFromBlackSource -Source $square150BlackSource -Destination $square150ForDarkShellTheme
$storeLogoForDarkShellTheme = Join-Path $assets 'StoreLogo.png'; $storeLogoForLightShellTheme = Join-Path $assets 'StoreLogo.theme-light.png'
$storeLogoBlackSource = if (Test-Path -LiteralPath $storeLogoForLightShellTheme -PathType Leaf) { $storeLogoForLightShellTheme } else { $storeLogoForDarkShellTheme }
if ($storeLogoBlackSource -ne $storeLogoForLightShellTheme) { Copy-Item -LiteralPath $storeLogoBlackSource -Destination $storeLogoForLightShellTheme -Force }; Set-WhiteGlyphFromBlackSource -Source $storeLogoBlackSource -Destination $storeLogoForDarkShellTheme
$icoSizes = 16, 20, 24, 32, 40, 48, 64, 256
New-PngIco -PngPaths ($icoSizes | ForEach-Object { Join-Path $assets "Square44x44Logo.targetsize-$_`_altform-unplated.png" }) -Destination (Join-Path $assets 'WindowIcon-ForDarkTheme.ico')
New-PngIco -PngPaths ($icoSizes | ForEach-Object { Join-Path $assets "Square44x44Logo.targetsize-$_`_altform-unplated_theme-light.png" }) -Destination (Join-Path $assets 'WindowIcon-ForLightTheme.ico')
