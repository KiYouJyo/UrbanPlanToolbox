# Contributing Guide

[简体中文](CONTRIBUTING.md) | [日本語](CONTRIBUTING.ja.md)

Thank you for contributing issues, documentation improvements, and well-described code changes. Please describe an issue or proposal in an Issue before opening a pull request.

## Scope

We prioritize reproducible defects, privacy or data-security issues, corrections to existing documentation, and improvements aligned with the current roadmap. For a new tool or data source, describe the user scenario, data boundaries, backup impact, and required trilingual copy first.

## Issues and pull requests

An Issue should include the app version, installation channel, Windows version, reproduction steps, expected result, and actual result. A pull request should describe its scope, validation, and whether local data is affected. Before submitting, run the tests, Debug/Release x64 builds, and `git diff --check`.

## Branches and commits

Create a topic branch from `main`, and use concise Conventional Commits-style messages such as `docs: ...`, `fix: ...`, or `feat: ...`. Do not modify `main` directly.

## Build and test

```powershell
dotnet restore UrbanPlanToolbox.slnx -p:Configuration=Debug -p:Platform=x64
dotnet test tests/UrbanPlanToolbox.Tests/UrbanPlanToolbox.Tests.csproj -c Debug -p:Platform=x64
dotnet build UrbanPlanToolbox.slnx -c Release -p:Platform=x64 --no-restore
```

The trilingual resources in `Strings/zh-CN`, `Strings/ja-JP`, and `Strings/en-US` must keep identical key sets. New tools should follow the [tool development template](docs/TOOL_DEVELOPMENT_TEMPLATE.en.md) and integrate with registration, navigation, search, favorites, storage, backup, and localization contracts.

## Data and secrets

Do not commit certificates, private keys, PFX files, tokens, user data, machine-specific paths, MSIX packages, or other build artifacts. The application is local-first and does not upload user data automatically.

## Community documents

- [Security Policy](SECURITY.en.md)
- [Code of Conduct](CODE_OF_CONDUCT.en.md)
