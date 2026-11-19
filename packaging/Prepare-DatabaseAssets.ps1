$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$destination = Join-Path $root 'Assets/Database/Japan'
$resources = Get-Content (Join-Path $PSScriptRoot 'database-assets.json') -Raw | ConvertFrom-Json
foreach ($resource in $resources) {
    $path = Join-Path $destination $resource.path
    if ((Test-Path $path) -and (Get-FileHash $path -Algorithm SHA256).Hash -eq $resource.sha256) { continue }
    New-Item (Split-Path $path -Parent) -ItemType Directory -Force | Out-Null
    $temporary = "$path.$([guid]::NewGuid().ToString('N')).tmp"
    try {
        Invoke-WebRequest -Uri $resource.url -OutFile $temporary -TimeoutSec 180 -MaximumRetryCount 3
        if ((Get-FileHash $temporary -Algorithm SHA256).Hash -ne $resource.sha256) { throw "Database asset hash mismatch: $($resource.path)" }
        Move-Item $temporary $path -Force
    } finally { if (Test-Path $temporary) { Remove-Item $temporary -Force } }
}
