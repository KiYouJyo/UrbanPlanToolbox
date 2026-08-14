English | [简体中文](STORE-PUBLISHING.md) | [日本語](STORE-PUBLISHING.ja.md)

# Microsoft Store publishing contract

Current Store status is defined in [project-status.json](project-status.json). Partner Center and actual Store availability are the final authorities for public publication; do not infer publication from repository metadata or a successful submission command.

## Store identity and package

- Store ID: `9MWDPJG1BHKW`
- Package identity: `JoKiy.UrbanPlanToolbox`
- Publisher: `CN=C4E4B33A-7B77-4121-897C-7D720A5471F8`
- Package family name: `JoKiy.UrbanPlanToolbox_4wdwgytaw3v2m`
- Manifest: `Package.Store.appxmanifest`
- Distribution channel: `Store`

Use the Store manifest and Store package only for the Store channel. The GitHub sideload identity, publisher, signing chain, package, and updater are independent and cannot provide cross-channel upgrades.

## WinGet / Microsoft Store source

Windows Package Manager's default `msstore` source reads from the Microsoft Store catalog. UrbanPlanToolbox therefore exposes the Store edition through its Store product ID:

```powershell
winget install --id 9MWDPJG1BHKW --source msstore -e
```

This is not a third package identity and does not require a separate WinGet publishing workflow. Installations performed through `msstore` remain Microsoft Store installations and continue to receive Store-managed updates. Availability follows the actual Store catalog state.

Do not submit the current GitHub sideload `.msixbundle` directly to the WinGet Community Repository. That package uses the project's self-signed certificate and relies on the first-install bootstrap to establish trust; the WinGet Community path does not perform that trust setup, and script-based bootstrap installers are not accepted as community installers. Reconsider a separate `winget` Community package only if a future installer can be trusted and installed silently on a clean system without that bootstrap.

## Authorized workflow

The workflow entry point is `.github/workflows/publish-microsoft-store.yml` and must be explicitly authorized for each Store submission. It verifies the approved source commit, version alignment, release notes, package identity, publisher, resources, and package evidence before interacting with Partner Center.

## Submission lifecycle

Prepare and validate the Store package, then submit only with explicit authorization. Treat submission, certification, and public availability as different states. Record a submission as `certification-submitted`; use `published` only after Partner Center and Store availability confirm public release.

## Failure recovery

Do not overwrite or delete an unknown pending submission. Read Partner Center first, preserve submission and package evidence, and diagnose the exact state before retrying. Failed certification, publication, upload, or unknown states are failures, not successful publication.

## WACK and secrets

Run WACK / Store technical validation for the final authorized package when required. Do not claim it passed without recorded evidence. Store credentials belong in approved secrets only; never commit certificates, private keys, client secrets, tokens, local packages, or diagnostic exports.

## Version and channel rules

The product version and both manifest package versions must align with release metadata before an authorized release. Store package versions must be valid and monotonic relative to Partner Center. Store submissions can be less frequent than GitHub releases and are never implied by a GitHub release. WinGet `msstore` availability follows the Store catalog and is not a separate release-state authority.
