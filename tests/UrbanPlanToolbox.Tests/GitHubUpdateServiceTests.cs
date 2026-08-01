using System.Net;
using System.Text;
using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Services;
using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class GitHubUpdateServiceTests
{
    [Theory]
    [InlineData("v0.3.0")]
    [InlineData("V0.3.0")]
    [InlineData("0.3.0.0")]
    [InlineData("v0.3.0-beta.1")]
    public void ParsesSupportedTagsToFourPartVersion(string tag)
    {
        Assert.True(VersionParser.TryParseTag(tag, out var version));
        Assert.Equal(new Version(0, 3, 0, 0), version);
    }

    [Theory]
    [InlineData("")]
    [InlineData("release-0.3.0")]
    [InlineData("0.3")]
    [InlineData("v0.3.0.0.1")]
    public void RejectsEmptyInvalidAndIncompleteTags(string tag) => Assert.False(VersionParser.TryParseTag(tag, out _));

    [Theory]
    [InlineData("0.3.0", "0.3.0.0", 0)]
    [InlineData("0.3.0", "0.2.0", 1)]
    [InlineData("0.10.0", "0.9.9", 1)]
    [InlineData("1.0.0", "0.99.99", 1)]
    public void ComparesNumericVersions(string left, string right, int expected)
    {
        Assert.True(VersionParser.TryParseTag(left, out var leftVersion));
        Assert.True(VersionParser.TryParseTag(right, out var rightVersion));
        Assert.Equal(expected, Math.Sign(leftVersion.CompareTo(rightVersion)));
    }

    [Theory]
    [InlineData("0.3.0", UpdateCheckStatus.UpToDate)]
    [InlineData("0.3.1", UpdateCheckStatus.UpdateAvailable)]
    [InlineData("0.2.0", UpdateCheckStatus.LocalVersionNewer)]
    public async Task ClassifiesRemoteVersionAgainstLocalVersion(string remoteTag, UpdateCheckStatus expected)
    {
        var result = await CreateService(HttpStatusCode.OK, ReleaseJson(remoteTag)).CheckForUpdatesAsync(new Version(0, 3, 0, 0));
        Assert.Equal(expected, result.Status);
    }

    [Fact] public async Task MissingTagNameIsInvalidResponse() => Assert.Equal(UpdateCheckStatus.InvalidResponse, (await CreateService(HttpStatusCode.OK, "{\"html_url\":\"https://github.com/KiYouJyo/UrbanPlanToolbox/releases/tag/v0.3.0\"}").CheckForUpdatesAsync(new Version(0, 3, 0, 0))).Status);
    [Fact] public async Task MissingHtmlUrlIsInvalidResponse() => Assert.Equal(UpdateCheckStatus.InvalidResponse, (await CreateService(HttpStatusCode.OK, "{\"tag_name\":\"v0.3.0\"}").CheckForUpdatesAsync(new Version(0, 3, 0, 0))).Status);
    [Fact] public async Task InvalidTagIsNotReportedAsUpdate() => Assert.Equal(UpdateCheckStatus.InvalidRemoteVersion, (await CreateService(HttpStatusCode.OK, ReleaseJson("newest")).CheckForUpdatesAsync(new Version(0, 3, 0, 0))).Status);
    [Fact] public async Task EmptyReleaseNotesUseEmptyString() => Assert.Equal(string.Empty, (await CreateService(HttpStatusCode.OK, ReleaseJson("v0.3.1", null)).CheckForUpdatesAsync(new Version(0, 3, 0, 0))).Release!.Body);

    [Theory]
    [InlineData(HttpStatusCode.NotFound, UpdateCheckStatus.NoRelease)]
    [InlineData(HttpStatusCode.InternalServerError, UpdateCheckStatus.RequestFailed)]
    [InlineData(HttpStatusCode.TooManyRequests, UpdateCheckStatus.RateLimited)]
    public async Task NonSuccessResponsesAreNotReportedAsUpToDate(HttpStatusCode statusCode, UpdateCheckStatus expected) => Assert.Equal(expected, (await CreateService(statusCode, "{}").CheckForUpdatesAsync(new Version(0, 3, 0, 0))).Status);

    [Fact]
    public async Task SendsCurrentApplicationUserAgent()
    {
        var handler = new StubHandler(HttpStatusCode.OK, ReleaseJson("v0.3.9"));
        await new GitHubUpdateService(new HttpClient(handler)).CheckForUpdatesAsync(new Version(0, 3, 8, 0));
        Assert.Equal("UrbanPlanToolbox/0.3.11", handler.UserAgent);
    }

    private static GitHubUpdateService CreateService(HttpStatusCode statusCode, string content) => new(new HttpClient(new StubHandler(statusCode, content)));
    private static string ReleaseJson(string tag, string? body = "Notes") => $"{{\"tag_name\":\"{tag}\",\"name\":\"{tag}\",\"body\":{(body is null ? "null" : $"\"{body}\"")},\"html_url\":\"https://github.com/KiYouJyo/UrbanPlanToolbox/releases/tag/{tag}\",\"published_at\":\"2026-07-30T00:00:00Z\"}}";

    private sealed class StubHandler(HttpStatusCode statusCode, string content) : HttpMessageHandler
    {
        public string? UserAgent { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            UserAgent = request.Headers.UserAgent.ToString();
            return Task.FromResult(new HttpResponseMessage(statusCode) { Content = new StringContent(content, Encoding.UTF8, "application/json") });
        }
    }
}
