# UrbanPlanToolbox v1.5.6

## GitHub in-app updates

GitHub installations now support user-triggered in-app update checks through Windows App Installer. After installation, users can check, download, and install later versions from About without manually opening GitHub Releases.

## First-time installation

Because the GitHub package uses the existing self-signed `CN=AppPublisher` certificate, first-time users should use the one-click installer package from the v1.5.6 GitHub Release. It validates the package, imports only the matching public certificate when needed, and then starts Windows App Installer through the stable App Installer URI.

The one-click installer does not use `Add-AppxPackage` for normal application installation. Windows App Installer performs the actual package installation and creates the update association.

## Update channels

Microsoft Store installations continue to use Microsoft Store updates. GitHub installations use App Installer updates. No background, launch-time, or unattended update checks are enabled in v1.5.6.
