using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.Services;

public static class AppUpdateServiceFactory
{
    public static IAppUpdateService CreateDefault()
    {
#if DEBUG
        var scenario = Environment.GetEnvironmentVariable("URBANPLANTOOLBOX_FAKE_UPDATE_SCENARIO");
        if (Enum.TryParse<FakeAppUpdateScenario>(scenario, true, out var parsed)) return new FakeAppUpdateService(parsed);
#endif
        var channelService = new AppDistributionChannelService();
        return channelService.GetCurrentChannel() == DistributionChannel.Store
            ? new StoreAppUpdateService(channelService, GetMainWindowHandle)
            : new GitHubAppUpdateService(new GitHubUpdateService());
    }

    private static nint? GetMainWindowHandle()
    {
        if (App.MainWindow is null) return null;
        return WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
    }
}
