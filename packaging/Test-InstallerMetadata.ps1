[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'payload\InstallerMetadata.ps1')
$root = Join-Path ([IO.Path]::GetTempPath()) ("UrbanPlanToolbox-metadata-test-" + [guid]::NewGuid().ToString('N'))
try {
    $v020 = Get-InstallerReleaseNames '0.2.0' '0.2.0.0'; $v021 = Get-InstallerReleaseNames '0.2.1' '0.2.1.0'
    if ($v020.ReleaseDirectoryName -ne 'UrbanPlanToolbox-v0.2.0-x64-framework-dependent-self-signed' -or $v020.MsixFileName -ne 'UrbanPlanToolbox_0.2.0.0_x64_framework-dependent_self-signed.msix' -or $v021.MsixFileName -ne 'UrbanPlanToolbox_0.2.1.0_x64_framework-dependent_self-signed.msix') { throw 'Versioned release naming test failed.' }
    New-Item -ItemType Directory -Path $root | Out-Null
    $valid = @{ schemaVersion=1; displayVersion='0.2.0'; packageVersion='0.2.0.0'; packageIdentityName='556F80C5-C4D4-452B-93B4-00DE3FA7AC29'; publisher='CN=AppPublisher'; architecture='x64'; msixFileName=$v020.MsixFileName; certificateFileName=$v020.CertificateFileName }
    $valid | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $root 'InstallerMetadata.json') -Encoding UTF8
    $metadata = Get-InstallerMetadata $root
    if ($metadata.packageVersion -cne '0.2.0.0' -or (Get-SafePayloadFilePath $root $metadata.msixFileName) -notlike "$root*") { throw 'Valid metadata test failed.' }
    foreach ($mutation in @(@{schemaVersion=2}, @{packageVersion='0.2.1.0'}, @{msixFileName='..\escape.msix'}, @{certificateFileName='C:\escape.cer'})) {
        $candidate = @{} + $valid; foreach ($key in $mutation.Keys) { $candidate[$key] = $mutation[$key] }; $candidate | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $root 'InstallerMetadata.json') -Encoding UTF8
        $failed = $false; try { $candidateMetadata = Get-InstallerMetadata $root; if ($mutation.ContainsKey('msixFileName')) { Get-SafePayloadFilePath $root $candidateMetadata.msixFileName | Out-Null }; if ($mutation.ContainsKey('certificateFileName')) { Get-SafePayloadFilePath $root $candidateMetadata.certificateFileName | Out-Null } } catch { $failed = $true }; if (-not $failed) { throw "Invalid metadata was accepted: $($mutation.Keys -join ',')" }
    }
    Write-Output 'InstallerMetadata controlled test passed.'
}
finally { if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force } }
