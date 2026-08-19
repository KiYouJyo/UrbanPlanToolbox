param(
    [string]$CatalogUrl = 'https://raw.githubusercontent.com/KiYouJyo/UrbanPlanToolbox_Data/main/catalog/catalog-v1.json',
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\Assets\DataPacks\Bundled')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$requiredPackIds = @(
    'planning-regulations',
    'planning-terminology',
    'design-concepts'
)
$officialReleasePrefix = 'https://github.com/KiYouJyo/UrbanPlanToolbox_Data/releases/download/'
$projectPath = Join-Path $PSScriptRoot '..\UrbanPlanToolbox.csproj'

function Get-ProjectVersion {
    [xml]$project = Get-Content -LiteralPath $projectPath -Raw
    $node = $project.SelectSingleNode('/Project/PropertyGroup/Version')
    if ($null -eq $node -or [string]::IsNullOrWhiteSpace($node.InnerText)) {
        throw 'UrbanPlanToolbox.csproj does not declare Version.'
    }
    return [Version]::Parse($node.InnerText.Trim())
}

function Get-CatalogSize([object]$pack) {
    if ($null -ne $pack.PSObject.Properties['sizeBytes'] -and $null -ne $pack.sizeBytes) { return [Int64]$pack.sizeBytes }
    if ($null -ne $pack.PSObject.Properties['size'] -and $null -ne $pack.size) { return [Int64]$pack.size }
    return 0L
}

function Test-ArchiveManifest([string]$archivePath, [object]$pack, [Version]$appVersion) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($archivePath)
    try {
        $manifestEntry = $archive.Entries | Where-Object FullName -ceq 'manifest.json' | Select-Object -First 1
        if ($null -eq $manifestEntry) { throw "Bundled data pack does not contain manifest.json: $archivePath" }
        $reader = [System.IO.StreamReader]::new($manifestEntry.Open())
        try { $manifest = $reader.ReadToEnd() | ConvertFrom-Json -ErrorAction Stop }
        finally { $reader.Dispose() }

        if ([string]$manifest.id -cne [string]$pack.id) { throw "Manifest pack ID mismatch for $($pack.id)." }
        if ([string]$manifest.version -cne [string]$pack.version) { throw "Manifest version mismatch for $($pack.id)." }
        if ([int]$manifest.schemaVersion -ne [int]$pack.schemaVersion) { throw "Manifest schema mismatch for $($pack.id)." }
        if ([string]$manifest.publisher -cne 'UrbanPlanToolbox') { throw "Unexpected manifest publisher for $($pack.id)." }
        if ([string]$manifest.formatVersion -ne '1') { throw "Unsupported data-pack format for $($pack.id)." }
        if (-not [string]::IsNullOrWhiteSpace([string]$manifest.minAppVersion) -and [Version]::Parse([string]$manifest.minAppVersion) -gt $appVersion) {
            throw "$($pack.id) requires UrbanPlanToolbox $($manifest.minAppVersion), newer than package version $appVersion."
        }
    }
    finally {
        $archive.Dispose()
    }
}

$appVersion = Get-ProjectVersion
$output = [IO.Path]::GetFullPath($OutputDirectory)
$parent = Split-Path -Parent $output
New-Item -ItemType Directory -Force -Path $parent | Out-Null
$temp = Join-Path $parent ('.bundled-data-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $temp | Out-Null

try {
    $catalogPath = Join-Path $temp 'catalog-v1.json'
    Invoke-WebRequest -Uri $CatalogUrl -OutFile $catalogPath -MaximumRedirection 5
    $catalog = Get-Content -LiteralPath $catalogPath -Raw | ConvertFrom-Json -ErrorAction Stop
    if ([string]$catalog.catalogVersion -cne '1') { throw "Unsupported data-pack catalog version: $($catalog.catalogVersion)" }

    $packs = @($catalog.packs)
    foreach ($packId in $requiredPackIds) {
        $matches = @($packs | Where-Object { [string]$_.id -ceq $packId })
        if ($matches.Count -ne 1) { throw "Catalog must contain exactly one '$packId' entry; found $($matches.Count)." }
        $pack = $matches[0]

        if ([int]$pack.schemaVersion -ne 1) { throw "$packId uses unsupported schema $($pack.schemaVersion)." }
        if ([Version]::Parse([string]$pack.minAppVersion) -gt $appVersion) {
            throw "$packId requires UrbanPlanToolbox $($pack.minAppVersion), newer than package version $appVersion."
        }

        $downloadUrl = [string]$pack.downloadUrl
        if (-not $downloadUrl.StartsWith($officialReleasePrefix, [StringComparison]::Ordinal)) {
            throw "$packId does not point at the official immutable data-pack release path."
        }
        $uri = [Uri]$downloadUrl
        $fileName = [Uri]::UnescapeDataString([IO.Path]::GetFileName($uri.AbsolutePath))
        if ($fileName -cne "$packId-$($pack.version).uptdata") {
            throw "Unexpected data-pack file name for ${packId}: $fileName"
        }

        $destination = Join-Path $temp $fileName
        Invoke-WebRequest -Uri $downloadUrl -OutFile $destination -MaximumRedirection 5

        $expectedSize = Get-CatalogSize $pack
        $actualSize = (Get-Item -LiteralPath $destination).Length
        if ($expectedSize -gt 0 -and $actualSize -ne $expectedSize) {
            throw "$packId size mismatch. Expected $expectedSize, got $actualSize."
        }

        $expectedHash = ([string]$pack.sha256).ToLowerInvariant()
        if ($expectedHash -notmatch '^[a-f0-9]{64}$') { throw "$packId catalog SHA-256 is invalid." }
        $actualHash = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actualHash -cne $expectedHash) { throw "$packId SHA-256 mismatch." }

        Test-ArchiveManifest -archivePath $destination -pack $pack -appVersion $appVersion
        Write-Host "Bundled $packId $($pack.version) ($actualSize bytes, sha256:$actualHash)"
    }

    $unexpected = @($packs | Where-Object { $requiredPackIds -notcontains [string]$_.id })
    if ($unexpected.Count -gt 0) {
        Write-Host "Catalog also contains $($unexpected.Count) unrelated pack(s); only the three application libraries are bundled."
    }

    if (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output -Recurse -Force }
    Move-Item -LiteralPath $temp -Destination $output
    Write-Host "Bundled data-pack directory is ready: $output"
}
finally {
    if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Recurse -Force }
}
