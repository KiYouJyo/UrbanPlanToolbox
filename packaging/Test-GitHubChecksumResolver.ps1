[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'payload\ChecksumResolver.ps1')
$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('UrbanPlanToolbox-checksum-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null
try {
    $fileName = 'UrbanPlanToolbox_1.5.8.0_x64.msixbundle'
    $hash = (('a' * 64) -join '').ToUpperInvariant()
    foreach ($line in @(
        "$hash  $fileName",
        "$hash *$fileName",
        (([char]0xFEFF).ToString() + "$hash  $fileName")
    )) {
        $path = Join-Path $tempRoot ([Guid]::NewGuid().ToString('N') + '.txt')
        [IO.File]::WriteAllText($path, $line + "`r`n", [Text.UTF8Encoding]::new($false))
        if ((Resolve-Sha256ManifestHash $path $fileName) -ne $hash) { throw "Standard checksum line did not parse: $line" }
    }
    $crlfPath = Join-Path $tempRoot 'crlf.txt'
    [IO.File]::WriteAllText($crlfPath, "$hash  $fileName`r`n", [Text.UTF8Encoding]::new($false))
    if ((Resolve-Sha256ManifestHash $crlfPath $fileName) -ne $hash) { throw 'CRLF checksum line did not parse.' }
    foreach ($line in @("$hash  wrong.msixbundle", ((('a' * 63) -join '') + "  $fileName"), ((('g' * 64) -join '') + "  $fileName"))) {
        $path = Join-Path $tempRoot ([Guid]::NewGuid().ToString('N') + '.txt')
        [IO.File]::WriteAllText($path, $line + "`n", [Text.UTF8Encoding]::new($false))
        if (Resolve-Sha256ManifestHash $path $fileName) { throw "Invalid checksum line matched: $line" }
    }
    if ((Get-ValidSha256Digest 'sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa') -ne $hash) { throw 'Lowercase sha256 digest did not parse.' }
    if ((Get-ValidSha256Digest 'SHA256:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA') -ne $hash) { throw 'Uppercase SHA256 digest did not parse.' }
    foreach ($digest in @('sha512:' + (('a' * 128) -join ''), 'sha256:bad', $null)) {
        if (Get-ValidSha256Digest $digest) { throw "Invalid digest matched: $digest" }
    }
    Write-Output 'GitHub checksum resolver tests passed.'
}
finally { if (Test-Path -LiteralPath $tempRoot) { Remove-Item -LiteralPath $tempRoot -Recurse -Force } }
