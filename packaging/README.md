# Packaging guidance

## Package identities

GitHub sideload and Microsoft Store packages are intentionally separate Windows identities. Never use one channel to detect, update, or remove the other.

| Channel | Package name | Publisher |
| --- | --- | --- |
| GitHub sideload | `556F80C5-C4D4-452B-93B4-00DE3FA7AC29` | `CN=AppPublisher` |
| Microsoft Store | `JoKiy.UrbanPlanToolbox` | `CN=C4E4B33A-7B77-4121-897C-7D720A5471F8` |

GitHub updater acceptance must query the GitHub sideload identity exactly. The Store identity may coexist on the same machine and must not be uninstalled as test-environment cleanup.

## GitHub one-click bootstrap

`New-GitHubOneClickInstallerPackage.ps1` creates a lightweight online bootstrap, not an offline package. It carries only scripts, metadata, the public certificate, and payload checksums. It never carries an MSIXBundle or `.appinstaller`.

The user-facing root layout is an international distribution contract and must use ASCII/English filenames only:

```text
payload/
README.txt
1-Install-UrbanPlanToolbox.cmd
2-Uninstall-UrbanPlanToolbox.cmd
```

Do not reintroduce localized or non-ASCII root filenames. Localized guidance belongs inside `README.txt`; the filename itself remains stable and language-neutral. `New-GitHubOneClickInstallerPackage.ps1` and `Test-GitHubOneClickInstallerPackage.ps1` both enforce this contract so future release packaging fails if the root layout drifts.

The GitHub install path is:

```text
GitHub Releases API
  -> download SHA256SUMS.txt and the single MSIXBundle to %TEMP%
  -> verify SHA256 and CN=AppPublisher signature
  -> Add-AppxPackage -Path <local-msixbundle>
  -> verify installed package identity/version/architecture/status
```

The Release contains only:

```text
UrbanPlanToolbox-v<version>-x64-one-click.zip
UrbanPlanToolbox_<package-version>_x64.msixbundle
SHA256SUMS.txt
```

`docs/UrbanPlanToolbox.appinstaller` remains available at its stable Pages URI as legacy compatibility infrastructure. It is not used by the GitHub bootstrap or the GitHub in-app updater.

## Validation

```powershell
./packaging/Test-GitHubOneClickInstallerPackage.ps1 -ReleaseDirectory <bootstrap-root> -ZipPath <bootstrap-zip>
```

The bootstrap must remain lightweight and must not contain `.msix`, `.msixbundle`, `.appinstaller`, `.pfx`, or `.p12` files.
