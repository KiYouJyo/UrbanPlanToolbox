using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.Services;

public sealed class GitHubUpdateService
{
    private static readonly HttpClient SharedClient = CreateClient();
    private readonly HttpClient _httpClient;

    public GitHubUpdateService(HttpClient? httpClient = null) => _httpClient = httpClient ?? SharedClient;

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(Version localVersion, CancellationToken cancellationToken = default)
    {
        localVersion = VersionParser.Normalize(localVersion);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, RepositoryLinks.LatestReleaseApi);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            request.Headers.UserAgent.ParseAdd("UrbanPlanToolbox/0.4.3");
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound) return new(UpdateCheckStatus.NoRelease, localVersion);
            if (response.StatusCode == HttpStatusCode.TooManyRequests || response.Headers.TryGetValues("X-RateLimit-Remaining", out var values) && values.Contains("0")) return new(UpdateCheckStatus.RateLimited, localVersion);
            if (!response.IsSuccessStatusCode) return new(UpdateCheckStatus.RequestFailed, localVersion);

            var payload = await response.Content.ReadFromJsonAsync<ReleasePayload>(cancellationToken: cancellationToken);
            if (payload is null || string.IsNullOrWhiteSpace(payload.TagName) || string.IsNullOrWhiteSpace(payload.HtmlUrl) || !Uri.TryCreate(payload.HtmlUrl, UriKind.Absolute, out var releaseUri)) return new(UpdateCheckStatus.InvalidResponse, localVersion);
            if (!VersionParser.TryParseTag(payload.TagName, out var remoteVersion)) return new(UpdateCheckStatus.InvalidRemoteVersion, localVersion);

            var release = new GitHubRelease(payload.TagName, payload.Name ?? payload.TagName, payload.Body ?? string.Empty, releaseUri, payload.PublishedAt);
            var comparison = remoteVersion.CompareTo(localVersion);
            return comparison > 0 ? new(UpdateCheckStatus.UpdateAvailable, localVersion, remoteVersion, release) : comparison < 0 ? new(UpdateCheckStatus.LocalVersionNewer, localVersion, remoteVersion, release) : new(UpdateCheckStatus.UpToDate, localVersion, remoteVersion, release);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return new(UpdateCheckStatus.TimedOut, localVersion); }
        catch (HttpRequestException) { return new(UpdateCheckStatus.ConnectionFailed, localVersion); }
        catch (JsonException) { return new(UpdateCheckStatus.InvalidResponse, localVersion); }
    }

    private static HttpClient CreateClient() => new() { Timeout = TimeSpan.FromSeconds(15) };

    private sealed record ReleasePayload(
        [property: JsonPropertyName("tag_name")] string? TagName,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("body")] string? Body,
        [property: JsonPropertyName("html_url")] string? HtmlUrl,
        [property: JsonPropertyName("published_at")] DateTimeOffset? PublishedAt);
}
