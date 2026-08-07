namespace UrbanPlanToolbox.Services;

public enum DistributionChannel
{
    GitHub,
    Store,
    Development
}

public sealed record DistributionChannelContext(
    DistributionChannel Channel,
    string DisplayResourceKey,
    bool CanCheckForUpdates,
    bool CanSelfUpdate,
    bool CanOpenReleases)
{
    public static DistributionChannelContext For(DistributionChannel channel) => channel switch
    {
        DistributionChannel.Store => new(channel, "About_ChannelStore", true, true, false),
        DistributionChannel.GitHub => new(channel, "About_ChannelGitHub", true, false, true),
        _ => new(DistributionChannel.Development, "About_ChannelDevelopment", false, false, false)
    };
}

public static class DistributionChannelProvider
{
    public static DistributionChannel Current => new AppDistributionChannelService().GetCurrentChannel();

    public static DistributionChannelContext CurrentContext => DistributionChannelContext.For(Current);

    public static bool UsesGitHubUpdates => Current == DistributionChannel.GitHub;
}

public static class DistributionChannelDecision
{
    public static DistributionChannel ForBuild(bool storeBuild, bool packageIdentityAvailable) =>
        storeBuild ? DistributionChannel.Store :
        packageIdentityAvailable ? DistributionChannel.GitHub : DistributionChannel.Development;
}

public enum AppUpdateProviderKind
{
    Store,
    GitHub,
    Development
}

public static class AppUpdateProviderDecision
{
    public static AppUpdateProviderKind ForChannel(DistributionChannel channel) => channel switch
    {
        DistributionChannel.Store => AppUpdateProviderKind.Store,
        DistributionChannel.GitHub => AppUpdateProviderKind.GitHub,
        _ => AppUpdateProviderKind.Development
    };
}
