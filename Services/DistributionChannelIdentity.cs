namespace UrbanPlanToolbox.Services;

public static class DistributionChannelIdentity
{
    public const string StoreName = "JoKiy.UrbanPlanToolbox";
    public const string StorePublisher = "CN=C4E4B33A-7B77-4121-897C-7D720A5471F8";
    public const string StorePublisherId = "c4e4b33a7b774121897c7d720a5471f8";

    public static DistributionChannel Identify(string? name, string? publisher, string? publisherId) =>
        string.Equals(name, StoreName, StringComparison.Ordinal) &&
        string.Equals(publisher, StorePublisher, StringComparison.Ordinal) &&
        string.Equals(publisherId, StorePublisherId, StringComparison.OrdinalIgnoreCase)
            ? DistributionChannel.Store
            : DistributionChannel.GitHub;
}
