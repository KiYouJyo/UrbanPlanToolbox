using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.Services;

public static class AppUpdateServiceFactory
{
    public static AppUpdateProviderKind GetProviderKind(DistributionChannel channel) => AppUpdateProviderDecision.ForChannel(channel);

    public static IAppUpdateService CreateDefault()
    {
#if DEBUG
        var scenario = Environment.GetEnvironmentVariable("URBANPLANTOOLBOX_FAKE_UPDATE_SCENARIO");
        if (Enum.TryParse<FakeAppUpdateScenario>(scenario, true, out var parsed)) return new FakeAppUpdateService(parsed);
#endif
        var channelService = new AppDistributionChannelService();
        return CreateForChannel(channelService.GetCurrentChannel(), channelService);
    }

    private static IAppUpdateService CreateForChannel(DistributionChannel channel, AppDistributionChannelService channelService) => channel switch
        {
            DistributionChannel.Store => new StoreAppUpdateService(channelService, GetMainWindowHandle),
            DistributionChannel.GitHub => new GitHubAppUpdateService(new GitHubUpdateService()),
            _ => new DevelopmentAppUpdateService()
        };

    private static nint? GetMainWindowHandle()
    {
        if (App.MainWindow is null) return null;
        return WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
    }
}
