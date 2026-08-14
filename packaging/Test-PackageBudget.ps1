[CmdletBinding()] param([Parameter(Mandatory)][string]$PackagePath)
Set-StrictMode -Version Latest; $ErrorActionPreference='Stop'; $root=Split-Path -Parent $PSScriptRoot
$budget=Get-Content (Join-Path $root 'performance/budgets.json') -Raw|ConvertFrom-Json; $item=Get-Item -LiteralPath $PackagePath
if($item.Extension -ine '.msixbundle'){throw 'Package budget applies to an MSIXBundle.'}; if($item.Length -gt $budget.msixBundle.maximumBytes){throw "Bundle exceeds budget. Actual=$($item.Length) Maximum=$($budget.msixBundle.maximumBytes)"}; Write-Host "PASS: MSIXBundle bytes=$($item.Length) budget=$($budget.msixBundle.maximumBytes)"
