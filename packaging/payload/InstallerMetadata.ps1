$ErrorActionPreference = 'Stop'
function Get-InstallerReleaseNames([string]$DisplayVersion, [string]$PackageVersion) {
    if ($DisplayVersion -notmatch '^\d+\.\d+\.\d+$' -or $PackageVersion -notmatch '^\d+\.\d+\.\d+\.\d+$' -or -not $PackageVersion.StartsWith("$DisplayVersion.")) { throw 'Invalid version input.' }
    [pscustomobject]@{ ReleaseDirectoryName="UrbanPlanToolbox-v$DisplayVersion-x64-framework-dependent-self-signed"; MsixFileName="UrbanPlanToolbox_$PackageVersion`_x64_framework-dependent_self-signed.msix"; CertificateFileName="UrbanPlanToolbox-v$DisplayVersion-Framework-Dependent.cer" }
}
function Get-InstallerMetadata([string]$PayloadRoot) {
    $path = Join-Path $PayloadRoot 'InstallerMetadata.json'
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw 'Missing InstallerMetadata.json.' }
    try { $metadata = Get-Content -LiteralPath $path -Raw -Encoding UTF8 | ConvertFrom-Json } catch { throw 'Invalid InstallerMetadata.json.' }
    foreach ($field in @('schemaVersion','displayVersion','packageVersion','packageIdentityName','publisher','architecture','msixFileName','certificateFileName')) { if ($null -eq $metadata.$field -or [string]::IsNullOrWhiteSpace([string]$metadata.$field)) { throw "Missing metadata field: $field" } }
    if ([int]$metadata.schemaVersion -ne 1) { throw 'Unsupported metadata schemaVersion.' }
    if ($metadata.displayVersion -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid displayVersion.' }
    if ($metadata.packageVersion -notmatch '^\d+\.\d+\.\d+\.\d+$' -or -not $metadata.packageVersion.StartsWith("$($metadata.displayVersion).")) { throw 'Invalid or inconsistent packageVersion.' }
    if ($metadata.architecture -cne 'x64') { throw 'Unsupported architecture.' }
    $metadata
}
function Get-SafePayloadFilePath([string]$PayloadRoot, [string]$FileName) {
    if ([string]::IsNullOrWhiteSpace($FileName) -or [IO.Path]::IsPathRooted($FileName) -or $FileName.Contains('..') -or $FileName.IndexOfAny([char[]]'\\/') -ge 0) { throw 'Unsafe payload file name.' }
    $root = [IO.Path]::GetFullPath($PayloadRoot).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar; $path = [IO.Path]::GetFullPath((Join-Path $PayloadRoot $FileName))
    if (-not $path.StartsWith($root,[StringComparison]::OrdinalIgnoreCase)) { throw 'Payload path traversal.' }; $path
}
function Get-MsixPackageMetadata([string]$MsixPath) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($MsixPath)
    try {
        $entry = $archive.Entries | Where-Object { $_.FullName -ieq 'AppxManifest.xml' } | Select-Object -First 1
        if ($null -eq $entry) {
            $inner = $archive.Entries | Where-Object { $_.FullName -match '_x64\.msix$' } | Select-Object -First 1
            if ($null -eq $inner) { throw 'MSIX or MSIXBundle is missing an x64 package.' }
            $temporary = [IO.Path]::GetTempFileName()
            try {
                $source = $inner.Open(); $target = [IO.File]::Open($temporary, [IO.FileMode]::Create)
                try { $source.CopyTo($target) } finally { $target.Dispose(); $source.Dispose() }
                return Get-MsixPackageMetadata $temporary
            } finally { Remove-Item -LiteralPath $temporary -Force -ErrorAction SilentlyContinue }
        }
        $reader = [IO.StreamReader]::new($entry.Open())
        try { [xml]$xml = $reader.ReadToEnd() } finally { $reader.Dispose() }
    } finally { $archive.Dispose() }
    $identity = $xml.SelectSingleNode("/*[local-name()='Package']/*[local-name()='Identity']"); $application = $xml.SelectSingleNode("/*[local-name()='Package']/*[local-name()='Applications']/*[local-name()='Application']"); $runtime = $xml.SelectSingleNode("/*[local-name()='Package']/*[local-name()='Dependencies']/*[local-name()='PackageDependency'][@Name='Microsoft.WindowsAppRuntime.2']")
    if ($null -eq $identity -or $null -eq $application -or $null -eq $runtime) { throw 'MSIX metadata is incomplete.' }; [pscustomobject]@{Name=$identity.Name;Publisher=$identity.Publisher;Version=$identity.Version;Architecture=$identity.ProcessorArchitecture;AppId=$application.Id;RuntimeMinVersion=$runtime.MinVersion}
}
function Assert-MetadataMatchesMsix($Metadata,$MsixMetadata) { if($MsixMetadata.Name -cne $Metadata.packageIdentityName -or $MsixMetadata.Publisher -cne $Metadata.publisher -or $MsixMetadata.Version -cne $Metadata.packageVersion -or $MsixMetadata.Architecture -cne $Metadata.architecture){throw 'MSIX manifest does not match InstallerMetadata.json.'} }
