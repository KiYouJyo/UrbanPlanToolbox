namespace UrbanPlanToolbox.Services;

public enum DistributionChannel
{
    GitHub,
    Store
}

public static class DistributionChannelProvider
{
    public static DistributionChannel Current =>
#if URBANPLANTOOLBOX_STORE
        DistributionChannel.Store;
#else
        DistributionChannel.GitHub;
#endif

    public static bool UsesGitHubUpdates => Current == DistributionChannel.GitHub;
}
