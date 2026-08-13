namespace UrbanPlanToolbox.Services;

public static class DistributionChannelIdentity
{
    public const string GitHubSideloadName = "556F80C5-C4D4-452B-93B4-00DE3FA7AC29";
    public const string GitHubSideloadPublisher = "CN=AppPublisher";
    public const string StoreName = "JoKiy.UrbanPlanToolbox";
    public const string StorePublisher = "CN=C4E4B33A-7B77-4121-897C-7D720A5471F8";

    public static StoreIdentityValidationResult ValidateStoreIdentity(string? name, string? publisher)
    {
        if (name is null || publisher is null) return StoreIdentityValidationResult.PackageUnavailable;
        if (!string.Equals(name, StoreName, StringComparison.Ordinal)) return StoreIdentityValidationResult.NameMismatch;
        if (!string.Equals(publisher, StorePublisher, StringComparison.Ordinal)) return StoreIdentityValidationResult.PublisherMismatch;
        return StoreIdentityValidationResult.Valid;
    }
}

public enum StoreIdentityValidationResult
{
    Valid,
    NameMismatch,
    PublisherMismatch,
    PackageUnavailable,
    Unknown
}
