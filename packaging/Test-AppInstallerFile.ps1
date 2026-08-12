[CmdletBinding()]
param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)][string]$ExpectedVersion, [Parameter(Mandatory)][string]$ExpectedBundleFileName)
[xml]$document = Get-Content -Raw -LiteralPath $Path
$ns = [Xml.XmlNamespaceManager]::new($document.NameTable)
$ns.AddNamespace('ai', 'http://schemas.microsoft.com/appx/appinstaller/2017/2')
$root = $document.SelectSingleNode('/ai:AppInstaller', $ns)
$bundle = $document.SelectSingleNode('/ai:AppInstaller/ai:MainBundle', $ns)
if ($null -eq $root -or $null -eq $bundle) { throw 'App Installer XML is missing AppInstaller/MainBundle.' }
if ($root.Version -ne "$ExpectedVersion.0" -or $bundle.Version -ne "$ExpectedVersion.0") { throw 'App Installer version mismatch.' }
if ([IO.Path]::GetFileName(([Uri]$bundle.Uri).AbsolutePath) -ne $ExpectedBundleFileName) { throw 'MainBundle URI filename mismatch.' }
if ($bundle.Name -ne '556F80C5-C4D4-452B-93B4-00DE3FA7AC29' -or $bundle.Publisher -ne 'CN=AppPublisher') { throw 'Package identity or publisher mismatch.' }
Write-Output 'App Installer validation passed.'
