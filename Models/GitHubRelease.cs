namespace UrbanPlanToolbox.Models;

public sealed record GitHubRelease(
    string TagName,
    string Name,
    string Body,
    Uri HtmlUrl,
    DateTimeOffset? PublishedAt,
    IReadOnlyList<GitHubReleaseAsset>? Assets = null);

public sealed record GitHubReleaseAsset(string Name, Uri DownloadUri, long? Size, string? Digest);
