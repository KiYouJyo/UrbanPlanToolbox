[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$ReleaseDirectory
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path $ReleaseDirectory
$requiredRoot = @('① 安装规划工具箱.cmd', '② 卸载规划工具箱.cmd', '请先阅读.txt')
$requiredPayload = @('Install.ps1', 'Uninstall.ps1', 'InstallLauncher.ps1', 'UninstallLauncher.ps1', 'UrbanPlanToolbox_0.1.1.0_x64_framework-dependent_self-signed.msix', 'UrbanPlanToolbox-v0.1.1-Framework-Dependent-Preview-Test.cer', 'SHA256SUMS.txt', 'Dependencies\x64\Microsoft.WindowsAppRuntime.2.msix')
foreach ($file in $requiredRoot) {
    if (-not (Test-Path -LiteralPath (Join-Path $root $file) -PathType Leaf)) { throw "发行根目录缺少 $file" }
}
foreach ($file in $requiredPayload) {
    if (-not (Test-Path -LiteralPath (Join-Path $root "payload\$file") -PathType Leaf)) { throw "payload 缺少 $file" }
}
$checksumPath = Join-Path $root 'payload\SHA256SUMS.txt'
$checksums = @{}
Get-Content -LiteralPath $checksumPath | ForEach-Object {
    if ($_ -match '^(?<hash>[A-Fa-f0-9]{64}) \*(?<name>.+)$') { $checksums[$matches.name.Replace('/', '\')] = $matches.hash.ToUpperInvariant() }
}
foreach ($file in ($requiredPayload | Where-Object { $_ -ne 'SHA256SUMS.txt' })) {
    $normalized = $file.Replace('/', '\')
    if (-not $checksums.ContainsKey($normalized)) { throw "SHA256SUMS.txt 缺少 $normalized" }
    $actual = (Get-FileHash -LiteralPath (Join-Path $root "payload\$file") -Algorithm SHA256).Hash.ToUpperInvariant()
    if ($actual -ne $checksums[$normalized]) { throw "SHA-256 不匹配：$normalized" }
}
foreach ($file in $requiredRoot | Where-Object { $_ -like '*.cmd' }) {
    $bytes = [System.IO.File]::ReadAllBytes((Join-Path $root $file))
    if (($bytes | Where-Object { $_ -gt 127 }).Count -ne 0) { throw "$file 必须仅包含 ASCII 字符。" }
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) { throw "$file 不得包含 UTF-8 BOM。" }
    for ($index = 0; $index -lt $bytes.Length; $index++) {
        if ($bytes[$index] -eq 10 -and ($index -eq 0 -or $bytes[$index - 1] -ne 13)) { throw "$file 必须使用 CRLF 换行。" }
    }
}
$forbiddenRoot = @('*.msix', '*.cer', '*.pfx', '*.p12')
foreach ($pattern in $forbiddenRoot) {
    if (Get-ChildItem -LiteralPath $root -File -Filter $pattern) { throw "发行根目录不应包含 $pattern" }
}
foreach ($pattern in @('*.pfx', '*.p12', '*.pdb', '.git', 'bin', 'obj', '.vs')) {
    if (Get-ChildItem -LiteralPath $root -Recurse -Force | Where-Object { $_.Name -like $pattern }) { throw "发行目录不应包含 $pattern" }
}
Write-Output '发行目录结构检查通过。'
