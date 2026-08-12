using Windows.System;

namespace UrbanPlanToolbox.Services;

public static class AppInstallerMigrationService
{
    public static async Task<bool> LaunchAsync()
    {
        var protocolUri = new Uri($"ms-appinstaller:?source={Uri.EscapeDataString(RepositoryLinks.AppInstaller.ToString())}");
        return await Launcher.LaunchUriAsync(protocolUri);
    }
}
