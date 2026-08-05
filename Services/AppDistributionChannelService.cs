using Windows.ApplicationModel;

namespace UrbanPlanToolbox.Services;

public sealed class AppDistributionChannelService
{
    public DistributionChannel GetCurrentChannel()
    {
        try
        {
            var id = Package.Current.Id;
            return DistributionChannelIdentity.Identify(id.Name, id.Publisher, id.PublisherId);
        }
        catch (Exception) when (OperatingSystem.IsWindows()) { return DistributionChannel.GitHub; }
    }

}
