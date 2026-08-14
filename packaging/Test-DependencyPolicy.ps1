[CmdletBinding()] param()
Set-StrictMode -Version Latest; $ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot; $fail = [Collections.Generic.List[string]]::new(); $pass = [Collections.Generic.List[string]]::new()
function Check([bool]$ok,[string]$message) { if($ok){$pass.Add($message)}else{$fail.Add($message)} }
[xml]$project = Get-Content (Join-Path $root 'UrbanPlanToolbox.csproj') -Raw
$references = @($project.SelectNodes("//*[local-name()='PackageReference']") | ForEach-Object { [pscustomobject]@{ Name=$_.Include; Version=$_.Version } })
$inventory = Get-Content (Join-Path $root 'performance/dependencies.json') -Raw | ConvertFrom-Json
foreach($reference in $references) { $entry=@($inventory.dependencies|Where-Object name -eq $reference.Name); Check ($entry.Count -eq 1) "Inventory registers $($reference.Name)"; if($entry.Count -eq 1){Check ($entry[0].version -eq $reference.Version) "Inventory version matches $($reference.Name)"; Check ($entry[0].kind -in 'managed','native','tooling') "Inventory kind is valid for $($reference.Name)"} }
foreach($entry in $inventory.dependencies){ Check (@($references|Where-Object Name -eq $entry.name).Count -eq 1) "Inventory contains no stale dependency: $($entry.name)" }
foreach($entry in @($inventory.dependencies|Where-Object kind -eq 'native')) { Check ($inventory.nativeAllowlist -contains $entry.name) "Native dependency is allowlisted: $($entry.name)" }
foreach($path in 'App.xaml.cs','MainWindow.xaml.cs','Views/HomePage.xaml.cs') { $content=Get-Content (Join-Path $root $path) -Raw; Check ($content -notmatch 'OpenCvSharp|DrawingComparisonService|DifferenceAnalysisService|DrawingLoadService') "Startup surface does not initialize OpenCV: $path" }
foreach($line in $pass){Write-Host "PASS: $line"}; foreach($line in $fail){Write-Host "FAIL: $line" -ForegroundColor Red}; Write-Host "Dependency policy: $($pass.Count) PASS, $($fail.Count) FAIL"; if($fail.Count){exit 1}
