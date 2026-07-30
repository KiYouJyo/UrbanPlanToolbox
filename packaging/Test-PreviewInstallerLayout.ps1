[CmdletBinding()]
param([Parameter(Mandatory)][string]$ReleaseDirectory)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path $ReleaseDirectory).Path
$payload = Join-Path $root 'payload'
. (Join-Path $payload 'InstallerMetadata.ps1')
$metadata = Get-InstallerMetadata $payload
$msixPath = Get-SafePayloadFilePath $payload $metadata.msixFileName
$cerPath = Get-SafePayloadFilePath $payload $metadata.certificateFileName
$rootCommands = @(Get-ChildItem -LiteralPath $root -File -Filter '*.cmd')
$rootReadmes = @(Get-ChildItem -LiteralPath $root -File -Filter '*.txt')
if ($rootCommands.Count -ne 2 -or $rootReadmes.Count -ne 1) { throw 'Release root command or readme files are incomplete.' }
foreach ($file in @('Install.ps1','Uninstall.ps1','InstallLauncher.ps1','UninstallLauncher.ps1','InstallerMetadata.ps1','SHA256SUMS.txt','Dependencies\x64\Microsoft.WindowsAppRuntime.2.msix')) { if (-not (Test-Path -LiteralPath (Join-Path $payload $file) -PathType Leaf)) { throw "Missing payload file: $file" } }
if (-not (Test-Path -LiteralPath $msixPath -PathType Leaf) -or -not (Test-Path -LiteralPath $cerPath -PathType Leaf)) { throw 'Metadata-declared MSIX or certificate is missing.' }
$actual = Get-MsixPackageMetadata $msixPath; Assert-MetadataMatchesMsix $metadata $actual
$checksums = @{}; Get-Content -LiteralPath (Join-Path $payload 'SHA256SUMS.txt') | ForEach-Object { if ($_ -match '^(?<hash>[A-Fa-f0-9]{64}) \*(?<name>.+)$') { $checksums[$matches.name.Replace('/','\')] = $matches.hash.ToUpperInvariant() } }
Get-ChildItem -LiteralPath $payload -Recurse -File | Where-Object { $_.Name -ne 'SHA256SUMS.txt' } | ForEach-Object { $relative = $_.FullName.Substring($payload.Length).TrimStart('\'); if (-not $checksums.ContainsKey($relative)) { throw "SHA256SUMS.txt missing $relative" }; if ((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToUpperInvariant() -ne $checksums[$relative]) { throw "SHA-256 mismatch: $relative" } }
foreach ($file in $rootCommands) { [byte[]]$bytes = [IO.File]::ReadAllBytes($file.FullName); if ($bytes | Where-Object { $_ -gt 127 }) { throw "$($file.Name) must be ASCII." }; if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) { throw "$($file.Name) must not have a BOM." }; for ($i=0; $i -lt $bytes.Length; $i++) { if ($bytes[$i] -eq 10 -and ($i -eq 0 -or $bytes[$i-1] -ne 13)) { throw "$($file.Name) must use CRLF." } } }
foreach ($pattern in @('*.pfx','*.p12','*.pdb','.git','bin','obj','.vs')) { if (Get-ChildItem -LiteralPath $root -Recurse -Force | Where-Object { $_.Name -like $pattern }) { throw "Release directory must not contain $pattern" } }
if (Get-ChildItem -LiteralPath $root -File | Where-Object { $_.Extension -in @('.msix','.cer','.pfx','.p12') }) { throw 'Package or certificate files must remain under payload.' }
$versioned = Get-ChildItem -LiteralPath $payload -File | Where-Object { $_.Name -match 'UrbanPlanToolbox.*\d+\.\d+\.\d+' }
if ($versioned | Where-Object { $_.Name -notmatch [regex]::Escape($metadata.displayVersion) -and $_.Name -notmatch [regex]::Escape($metadata.packageVersion) }) { throw 'Found versioned UrbanPlanToolbox file inconsistent with metadata.' }
if ((Get-Content -LiteralPath $rootReadmes[0].FullName -Raw) -match '{{.+}}') { throw 'Readme still has unrendered placeholders.' }
Write-Output 'Release layout validation passed.'
