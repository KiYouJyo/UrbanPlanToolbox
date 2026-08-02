namespace UrbanPlanToolbox.Services;

public static class RepositoryLinks
{
    public static readonly Uri Repository = new("https://github.com/KiYouJyo/UrbanPlanToolbox");
    public static readonly Uri Issues = new("https://github.com/KiYouJyo/UrbanPlanToolbox/issues");
    public static readonly Uri LatestReleaseApi = new("https://api.github.com/repos/KiYouJyo/UrbanPlanToolbox/releases/latest");

    // This is compiled only into the Microsoft Store channel. The GitHub channel has no
    // Store update path and must retain its independent release identity.
    public static Uri? StoreProductUri =>
#if URBANPLANTOOLBOX_STORE
        new Uri("ms-windows-store://pdp/?productid=9MWDPJG1BHKW");
#else
        null;
#endif

    public static Uri? StoreWebUri =>
#if URBANPLANTOOLBOX_STORE
        new Uri("https://apps.microsoft.com/detail/9MWDPJG1BHKW");
#else
        null;
#endif
}
