using Windows.ApplicationModel;

namespace UrbanPlanToolbox.Services;

public sealed class AppDistributionChannelService
{
    public DistributionChannel GetCurrentChannel()
    {
#if URBANPLANTOOLBOX_STORE
        // The build channel is authoritative. Package identity is diagnostic only.
        return DistributionChannelDecision.ForBuild(storeBuild: true, packageIdentityAvailable: false);
#else
        try
        {
            _ = Package.Current.Id;
            return DistributionChannelDecision.ForBuild(storeBuild: false, packageIdentityAvailable: true);
        }
        catch (Exception) when (OperatingSystem.IsWindows()) { return DistributionChannelDecision.ForBuild(false, false); }
#endif
    }

    public StoreIdentityValidationResult GetStoreIdentityValidation()
    {
        try
        {
            var id = Package.Current.Id;
            return DistributionChannelIdentity.ValidateStoreIdentity(id.Name, id.Publisher);
        }
        catch (Exception) when (OperatingSystem.IsWindows()) { return StoreIdentityValidationResult.PackageUnavailable; }
    }

    public PackageIdentitySnapshot GetPackageIdentity()
    {
        try
        {
            var id = Package.Current.Id;
            return new(id.Name, id.Publisher, id.PublisherId, id.FamilyName, id.FullName);
        }
        catch (Exception) when (OperatingSystem.IsWindows()) { return PackageIdentitySnapshot.Unavailable; }
    }

    public DistributionChannelContext GetContext() => DistributionChannelContext.For(GetCurrentChannel());

}

public sealed record PackageIdentitySnapshot(string Name, string Publisher, string PublisherId, string FamilyName, string FullName)
{
    public static PackageIdentitySnapshot Unavailable { get; } = new("Unavailable", "Unavailable", "Unavailable", "Unavailable", "Unavailable");
}
