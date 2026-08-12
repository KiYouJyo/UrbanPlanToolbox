using Windows.System;

namespace UrbanPlanToolbox.Services;

public static class AppInstallerMigrationService
{
    public static async Task<bool> LaunchAsync()
    {
        return await Launcher.LaunchUriAsync(RepositoryLinks.AppInstaller);
    }
}
