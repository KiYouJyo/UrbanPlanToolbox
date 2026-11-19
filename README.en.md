<p align="center">
  <img src="docs/assets/toolbox-icon.png" width="128" height="128" alt="UrbanPlanToolbox">
</p>
<h1 align="center">UrbanPlanToolbox</h1>
<p align="center">An offline-first Windows toolbox for urban planning, architectural design, and spatial research.</p>
<p align="center">
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-yellow.svg" alt="MIT License"></a>
  <a href="https://github.com/KiYouJyo/UrbanPlanToolbox/actions/workflows/ci.yml"><img src="https://github.com/KiYouJyo/UrbanPlanToolbox/actions/workflows/ci.yml/badge.svg?branch=main" alt="CI"></a>
  <a href="https://github.com/KiYouJyo/UrbanPlanToolbox/releases/latest"><img src="https://img.shields.io/github/v/release/KiYouJyo/UrbanPlanToolbox?display_name=tag&amp;sort=semver" alt="GitHub Release"></a>
  <a href="https://github.com/KiYouJyo/UrbanPlanToolbox"><img src="https://img.shields.io/badge/platform-Windows%20%7C%20WinUI%203-0078D4?logo=windows" alt="Platform"></a>
  <a href="https://kiyoujyo.github.io/UrbanPlanToolbox/"><img src="https://img.shields.io/badge/Website-UrbanPlanToolbox-0078D4" alt="Website"></a>
</p>
<p align="center">
  <a href="https://get.microsoft.com/installer/download/9MWDPJG1BHKW?referrer=appbadge"><img src="https://get.microsoft.com/images/en-us%20dark.svg" width="240" alt="Get UrbanPlanToolbox from Microsoft Store"></a>
</p>
<p align="center"><a href="README.md">简体中文</a> | <a href="README.ja.md">日本語</a> | English</p>

## Get the app

- [Latest formal GitHub Release](https://github.com/KiYouJyo/UrbanPlanToolbox/releases/latest): more frequent x64 sideloaded releases.
- [Microsoft Store](https://apps.microsoft.com/detail/9MWDPJG1BHKW): for users who prefer Microsoft Store installation and updates.
- **WinGet (Microsoft Store source)**: install the Store edition directly from a terminal:

```powershell
winget install --id 9MWDPJG1BHKW --source msstore -e
```

The GitHub sideload edition and Microsoft Store edition have independent package identities and update paths and cannot upgrade over each other. The WinGet `msstore` entry is another installation entry point for the Microsoft Store edition, not a third package identity; later updates remain Store-managed. See [GitHub Releases](https://github.com/KiYouJyo/UrbanPlanToolbox/releases) and [CHANGELOG.md](CHANGELOG.md) for version details.

## Core features

- Design and research project management, project home, workspace, archive, and restore.
- Project milestones, local reminders, and work-folder access.
- Planning metrics, unit and scale conversion, palette records, and workflow checklists.
- Architecture and planning regulations, design-concept dictionary, local search, and favorites.
- Local WGS 84, GCJ-02, and BD-09 point conversion with Shapefile processing.
- Survey photo organization, EXIF/GPS reading, GIS points, and CSV export.
- Simplified Chinese, Japanese, and English; light, dark, and system themes.

## Installation and updates

### First GitHub installation

Download the lightweight one-click installer from the [latest GitHub Release](https://github.com/KiYouJyo/UrbanPlanToolbox/releases/latest). It completes the required certificate setup, obtains the current formal package online, and installs the app so later updates can be performed in-app.

### Later updates

Open About and choose Check for updates. GitHub builds can check for and download formal updates in the app; the app verifies the downloaded package's integrity and signature, then the user chooses restart and update once it is ready. Microsoft Store builds, including installations performed through WinGet's `msstore` source, continue to use the Store update channel.

### Advanced installation

Advanced users can obtain the `.msixbundle` and SHA-256 manifest from Release Assets for manual deployment or verification. The stable App Installer manifest is provided by the project Pages URL.

## Privacy and offline design

Core features require no account, cloud sync, or internet connection. Projects, tool data, and backups stay on the device by default; update checks access the relevant channel only after the user requests them. Photos, GPS, and coordinate data are processed locally.

## Requirements

Windows 10 17763 or later, x64.

## Data and backups

Settings supports `.uptbackup` export, import, and restore with a manifest and SHA-256 checks. Work-folder contents are not uploaded or copied into backups; imported backups require the folder to be selected again.

## Languages

The interface supports Simplified Chinese, 日本語, and English, with immediate language switching in Settings.

## Documentation

- [Roadmap and version policy](docs/ROADMAP.md)
- [Release guide](docs/RELEASE.md)
- [Documentation governance and current status](docs/DOCUMENTATION.md)
- [Data storage](docs/DATA_STORAGE.md) · [Data backup](docs/DATA_BACKUP.md)
- [Changelog](CHANGELOG.md)
- [Privacy policy](PRIVACY.md) · [Third-party notices](THIRD-PARTY-NOTICES.md)
- [Contributing guide](CONTRIBUTING.en.md) · [Security policy](SECURITY.en.md) · [Code of conduct](docs/community/CODE_OF_CONDUCT.en.md)

## Development and build

```powershell
dotnet restore UrbanPlanToolbox.slnx -p:Configuration=Debug -p:Platform=x64
dotnet test UrbanPlanToolbox.slnx -c Debug -p:Platform=x64 --no-restore
```

See [docs/RELEASE.md](docs/RELEASE.md) for the complete build and release process.

## Feedback

Report issues through [GitHub Issues](https://github.com/KiYouJyo/UrbanPlanToolbox/issues) or the [support page](https://kiyoujyo.github.io/UrbanPlanToolbox/support/). Remove local paths and personal data before sharing diagnostics.

## License

UrbanPlanToolbox is open source under the [MIT License](LICENSE).
