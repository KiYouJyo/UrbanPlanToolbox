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

    [Fact]
    public void UnpackagedDevelopmentDoesNotPretendToBeAGitHubInstall()
    {
        Assert.Equal("1.6.6", AppVersionProvider.Version);
        Assert.Equal("v1.6.6", AppVersionProvider.DisplayVersion);
        Assert.Equal(DistributionChannel.Development, DistributionChannelProvider.Current);
        Assert.False(DistributionChannelProvider.UsesGitHubUpdates);
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
        Assert.Equal("UrbanPlanToolbox/1.6.6", handler.UserAgent);
    }

    [Theory]
    [InlineData(true, "BundleSignatureVerified", "CN=AppPublisher", "BD85AD77A651C86CA01A480C8E9BC64952993F98", null)]
    [InlineData(false, "SignatureMissing", null, null, "SignatureMissing")]
    [InlineData(false, "SignatureInvalid", null, null, "SignatureInvalid")]
    [InlineData(true, "BundleSignatureVerified", "CN=WrongPublisher", "BD85AD77A651C86CA01A480C8E9BC64952993F98", "SignerSubjectMismatch")]
    [InlineData(true, "BundleSignatureVerified", "CN=AppPublisher", "0123456789ABCDEF0123456789ABCDEF01234567", "SignerThumbprintMismatch")]
    public async Task DownloadVerificationRequiresValidSignatureSubjectAndThumbprint(bool isValid, string verifierCode, string? subject, string? thumbprint, string? expectedFailure)
    {
        var bytes = Encoding.UTF8.GetBytes("test bundle bytes");
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
        var bundleName = "UrbanPlanToolbox_1.6.4.0_x64.msixbundle";
        var release = new GitHubRelease("v1.6.4", "v1.6.4", "", new Uri("https://github.com/KiYouJyo/UrbanPlanToolbox/releases/tag/v1.6.4"), null,
        [
            new(bundleName, new Uri("https://github.com/KiYouJyo/UrbanPlanToolbox/releases/download/v1.6.4/" + bundleName), bytes.Length, null),
            new("SHA256SUMS.txt", new Uri("https://github.com/KiYouJyo/UrbanPlanToolbox/releases/download/v1.6.4/SHA256SUMS.txt"), hash.Length + bundleName.Length + 4, null)
        ]);
        var handler = new BundleDownloadHandler(bytes, $"{hash}  {bundleName}\n");
        var service = new GitHubUpdateService(new HttpClient(handler), new FixedSignatureVerifier(new(isValid, verifierCode, subject, thumbprint)));

        var result = await service.DownloadAndVerifyBundleAsync(release, bundleName);

        Assert.Equal(expectedFailure, result.FailureCode);
        Assert.Equal(expectedFailure is null, result.BundlePath is not null);
        if (result.BundlePath is { } path && File.Exists(path)) File.Delete(path);
    }

    [Fact]
    public void OfficialV164BundlePassesTheWindowsVerifierWhenProvidedForIntegrationTesting()
    {
        var bundlePath = Environment.GetEnvironmentVariable("URBANPLANTOOLBOX_OFFICIAL_BUNDLE");
        if (string.IsNullOrWhiteSpace(bundlePath)) return;

        var result = new MsixBundleSignatureVerifier().Verify(bundlePath);

        Assert.True(result.IsValid, $"{result.FailureCode}; HRESULT=0x{result.HResult:X8}");
        Assert.Equal(GitHubUpdateService.ExpectedSignerSubject, result.SignerSubject);
        Assert.Equal(GitHubUpdateService.ExpectedSignerThumbprint, result.SignerThumbprint, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void TamperedBundleFailsTheWindowsVerifierWhenProvidedForIntegrationTesting()
    {
        var bundlePath = Environment.GetEnvironmentVariable("URBANPLANTOOLBOX_TAMPERED_BUNDLE");
        if (string.IsNullOrWhiteSpace(bundlePath)) return;

        var result = new MsixBundleSignatureVerifier().Verify(bundlePath);

        Assert.False(result.IsValid);
        Assert.Contains(result.FailureCode, new[] { "SignatureInvalid", "SignatureMissing" });
    }

    [Fact]
    public void UnsignedBundleFailsTheWindowsVerifier()
    {
        var bundlePath = Path.Combine(Path.GetTempPath(), $"UrbanPlanToolbox-unsigned-{Guid.NewGuid():N}.msixbundle");
        try
        {
            File.WriteAllBytes(bundlePath, [0x50, 0x4B, 0x03, 0x04]);

            var result = new MsixBundleSignatureVerifier().Verify(bundlePath);

            Assert.False(result.IsValid);
            Assert.Contains(result.FailureCode, new[] { "SignatureInvalid", "SignatureMissing" });
        }
        finally
        {
            if (File.Exists(bundlePath)) File.Delete(bundlePath);
        }
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

    private sealed class BundleDownloadHandler(byte[] bundle, string checksums) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(request.RequestUri!.AbsolutePath.EndsWith("SHA256SUMS.txt", StringComparison.Ordinal) ? Encoding.UTF8.GetBytes(checksums) : bundle)
            });
    }

    private sealed class FixedSignatureVerifier(BundleSignatureVerificationResult result) : IBundleSignatureVerifier
    {
        public BundleSignatureVerificationResult Verify(string bundlePath) => result;
    }
}
