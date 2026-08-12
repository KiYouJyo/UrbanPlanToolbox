using UrbanPlanToolbox.Services;

namespace UrbanPlanToolbox.Models;

public sealed record UpdateManifest(int SchemaVersion, UpdateManifestChannels Channels)
{
    public string? VersionFor(DistributionChannel channel) => channel switch
    {
        DistributionChannel.Store => Channels.Store?.Version,
        DistributionChannel.GitHub => Channels.GitHub?.Version,
        _ => null
    };
}

public sealed record UpdateManifestChannels(UpdateManifestChannel? GitHub, UpdateManifestChannel? Store);
public sealed record UpdateManifestChannel(string? Version);
