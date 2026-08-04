using Windows.ApplicationModel;

namespace UrbanPlanToolbox.Services;

public sealed class AppDistributionChannelService
{
    public DistributionChannel GetCurrentChannel()
    {
#if URBANPLANTOOLBOX_STORE
        try
        {
            var id = Package.Current.Id;
            // Partner Center identity is checked as a pair; a similarly named sideload package is not Store.
            return id.Name == "JoKiy.UrbanPlanToolbox" && id.PublisherId == "c4e4b33a7b774121897c7d720a5471f8"
                ? DistributionChannel.Store : DistributionChannel.GitHub;
        }
        catch (Exception) when (OperatingSystem.IsWindows()) { return DistributionChannel.GitHub; }
#else
        return DistributionChannel.GitHub;
#endif
    }
}
