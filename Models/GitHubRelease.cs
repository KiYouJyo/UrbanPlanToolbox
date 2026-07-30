namespace UrbanPlanToolbox.Models;

public sealed record GitHubRelease(string TagName, string Name, string Body, Uri HtmlUrl, DateTimeOffset? PublishedAt);
