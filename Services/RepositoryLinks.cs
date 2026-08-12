namespace UrbanPlanToolbox.Services;

public static class RepositoryLinks
{
    public static readonly Uri Repository = new("https://github.com/KiYouJyo/UrbanPlanToolbox");
    public static readonly Uri Issues = new("https://github.com/KiYouJyo/UrbanPlanToolbox/issues");
    public static readonly Uri Releases = new("https://github.com/KiYouJyo/UrbanPlanToolbox/releases");
    public static readonly Uri License = new("https://github.com/KiYouJyo/UrbanPlanToolbox/blob/main/LICENSE");
    public static readonly Uri PrivacyPolicy = new("https://kiyoujyo.github.io/UrbanPlanToolbox/privacy/");
    public static readonly Uri LatestReleaseApi = new("https://api.github.com/repos/KiYouJyo/UrbanPlanToolbox/releases/latest");
    public static readonly Uri AppInstaller = new("https://kiyoujyo.github.io/UrbanPlanToolbox/UrbanPlanToolbox.appinstaller");

    // Product URL is a published project link, not an update endpoint. Store updates use StoreContext.
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
