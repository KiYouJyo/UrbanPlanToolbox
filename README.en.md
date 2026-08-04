简体中文 | [日本語](README.ja.md) | English

# UrbanPlanToolbox

An offline-first Windows toolbox for urban planning, architectural design, and spatial research.

[![Microsoft Store](https://img.shields.io/badge/Microsoft%20Store-Download-0078D4?logo=microsoftstore&logoColor=white)](https://apps.microsoft.com/detail/9MWDPJG1BHKW) [![CI](https://github.com/KiYouJyo/UrbanPlanToolbox/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/KiYouJyo/UrbanPlanToolbox/actions/workflows/ci.yml) ![Version](https://img.shields.io/badge/version-1.1.0-0078D4)

## Get the app

The current version is **1.1.0**. Microsoft Store and GitHub sideload packages both use `1.1.0.0`, while their package identities and publishers remain separate.

## About UrbanPlanToolbox

UrbanPlanToolbox is a Windows desktop toolbox for urban planning, architectural design, and spatial research. Projects, tool data, and backups are kept on the device by default. Core features do not require an account, cloud sync, or an internet connection.

## Features

- Design and research project management, project home, workspace, archive, and restore.
- Project milestones with Windows local reminders and work-folder access.
- Planning-metrics calculator and unit and scale converter.
- Palette recorder, workflow review checklist, architecture and planning regulations index, and design-concept dictionary.
- Coordinate System Converter for local WGS 84, GCJ-02, and BD-09 point conversion. Shapefile files are processed locally; projected coordinate systems are not supported.
- Local tool search and favorites, plus `.uptbackup` export, import, and restore.
- Simplified Chinese, Japanese, and English; light, dark, and system themes.

## Installation

The Microsoft Store is the primary channel for ordinary users. The repository also provides an x64 framework-dependent self-signed sideloading package; obtain the matching version from [GitHub Releases](https://github.com/KiYouJyo/UrbanPlanToolbox/releases) and verify its checksums before installation. The two channel identities cannot update each other in place.

## Privacy and offline design

Microsoft Store updates are managed by Microsoft Store. The sideloaded build accesses the GitHub Releases API only when the user explicitly checks for updates; external regulations, support, and project links open only after the user selects them. The app requires no account and has no ads, telemetry, tracking, automatic crash upload, or automatic upload of user data. Project and tool data stays on the device.

GCJ-02 and BD-09 results use public approximation algorithms for map overlay, data preparation, and research support only. They are not surveying-, approval-, construction-, or legal-grade coordinate transformations.

See [PRIVACY.md](PRIVACY.md) and the [online privacy policy](https://kiyoujyo.github.io/UrbanPlanToolbox/privacy/).

## Requirements

Windows 10 17763 or later, x64. Development requires .NET 10, WinUI 3, Windows App SDK, and Windows SDK 10.0.26100.0.

## Data and backups

App data is stored in the local application-data directory. Settings provides `.uptbackup` export, import, and restore with a manifest and SHA-256 checks. Work-folder contents are not uploaded or copied into a backup; an imported backup requires the folder to be selected again.

## Languages

The interface supports Simplified Chinese, 日本語, and English. Choose a language in Settings; it takes effect after restart.

## Documentation

- [Roadmap and version policy](docs/ROADMAP.md)
- [Release guide](docs/RELEASE.md)
- [Microsoft Store publishing guide](docs/STORE-PUBLISHING.md)
- [Data storage](docs/DATA_STORAGE.md), [data backup](docs/DATA_BACKUP.md)
- [Changelog](CHANGELOG.md)
- [Third-party notices](THIRD-PARTY-NOTICES.md)

## Development and build

```powershell
dotnet restore UrbanPlanToolbox.slnx -p:Configuration=Debug -p:Platform=x64
dotnet test tests/UrbanPlanToolbox.Tests/UrbanPlanToolbox.Tests.csproj -c Debug -p:Platform=x64
```

See [docs/RELEASE.md](docs/RELEASE.md) for build, channel separation, WACK, and publishing procedures.

## Feedback

Report issues through [GitHub Issues](https://github.com/KiYouJyo/UrbanPlanToolbox/issues) or visit the [support page](https://kiyoujyo.github.io/UrbanPlanToolbox/support/). Remove local paths, project content, and personal data before sharing diagnostics.

## Roadmap

See [docs/ROADMAP.md](docs/ROADMAP.md) for completed work and future directions. The roadmap communicates direction and is not a promise of features or dates.

## License and third-party notices

This repository does not currently declare a project license. Until that decision is made, do not describe it as licensed open-source software. Dependencies and external data sources are listed in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
