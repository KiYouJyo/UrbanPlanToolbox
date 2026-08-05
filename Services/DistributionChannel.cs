namespace UrbanPlanToolbox.Services;

public enum DistributionChannel
{
    GitHub,
    Store
}

public static class DistributionChannelProvider
{
    public static DistributionChannel Current => new AppDistributionChannelService().GetCurrentChannel();

    public static bool UsesGitHubUpdates => Current == DistributionChannel.GitHub;
}
