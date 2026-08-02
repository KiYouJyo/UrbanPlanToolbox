namespace UrbanPlanToolbox.Services;

public static class RepositoryLinks
{
    public static readonly Uri Repository = new("https://github.com/KiYouJyo/UrbanPlanToolbox");
    public static readonly Uri Issues = new("https://github.com/KiYouJyo/UrbanPlanToolbox/issues");
    public static readonly Uri LatestReleaseApi = new("https://api.github.com/repos/KiYouJyo/UrbanPlanToolbox/releases/latest");
    // Set only after Partner Center supplies the real product URL. Never invent a Store ID.
    public static Uri? StoreProductUri => null;
}
