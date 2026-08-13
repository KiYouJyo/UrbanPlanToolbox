using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.Services;

public sealed class GitHubUpdateService
{
    public const string ExpectedSignerSubject = "CN=AppPublisher";
    public const string ExpectedSignerThumbprint = "BD85AD77A651C86CA01A480C8E9BC64952993F98";
    private static readonly HttpClient SharedClient = CreateClient();
    private readonly HttpClient _httpClient;
    private readonly IBundleSignatureVerifier _signatureVerifier;

    public GitHubUpdateService(HttpClient? httpClient = null, IBundleSignatureVerifier? signatureVerifier = null)
    {
        _httpClient = httpClient ?? SharedClient;
        _signatureVerifier = signatureVerifier ?? new MsixBundleSignatureVerifier();
    }

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(Version localVersion, CancellationToken cancellationToken = default)
    {
        localVersion = VersionParser.Normalize(localVersion);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, RepositoryLinks.LatestReleaseApi);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            request.Headers.UserAgent.ParseAdd($"UrbanPlanToolbox/{AppVersionProvider.Version}");
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound) return new(UpdateCheckStatus.NoRelease, localVersion);
            if (response.StatusCode == HttpStatusCode.TooManyRequests || response.Headers.TryGetValues("X-RateLimit-Remaining", out var values) && values.Contains("0")) return new(UpdateCheckStatus.RateLimited, localVersion);
            if (!response.IsSuccessStatusCode) return new(UpdateCheckStatus.RequestFailed, localVersion);

            var payload = await response.Content.ReadFromJsonAsync<ReleasePayload>(cancellationToken: cancellationToken);
            if (payload is null || string.IsNullOrWhiteSpace(payload.TagName) || string.IsNullOrWhiteSpace(payload.HtmlUrl) || !Uri.TryCreate(payload.HtmlUrl, UriKind.Absolute, out var releaseUri)) return new(UpdateCheckStatus.InvalidResponse, localVersion);
            if (!VersionParser.TryParseTag(payload.TagName, out var remoteVersion)) return new(UpdateCheckStatus.InvalidRemoteVersion, localVersion);

            var assets = payload.Assets?
                .Where(asset => !string.IsNullOrWhiteSpace(asset.Name) && Uri.TryCreate(asset.DownloadUrl, UriKind.Absolute, out _))
                .Select(asset => new GitHubReleaseAsset(asset.Name!, new Uri(asset.DownloadUrl!), asset.Size, asset.Digest))
                .ToArray();
            var release = new GitHubRelease(payload.TagName, payload.Name ?? payload.TagName, payload.Body ?? string.Empty, releaseUri, payload.PublishedAt, assets);
            var comparison = remoteVersion.CompareTo(localVersion);
            return comparison > 0 ? new(UpdateCheckStatus.UpdateAvailable, localVersion, remoteVersion, release) : comparison < 0 ? new(UpdateCheckStatus.LocalVersionNewer, localVersion, remoteVersion, release) : new(UpdateCheckStatus.UpToDate, localVersion, remoteVersion, release);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return new(UpdateCheckStatus.TimedOut, localVersion); }
        catch (HttpRequestException) { return new(UpdateCheckStatus.ConnectionFailed, localVersion); }
        catch (JsonException) { return new(UpdateCheckStatus.InvalidResponse, localVersion); }
    }

    public async Task<BundleDownloadVerificationResult> DownloadAndVerifyBundleAsync(
        GitHubRelease release,
        string expectedBundleFileName,
        IProgress<AppUpdateProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var bundleAssets = release.Assets?.Where(asset => asset.Name.EndsWith(".msixbundle", StringComparison.OrdinalIgnoreCase)).ToArray() ?? [];
        var checksumAsset = release.Assets?.SingleOrDefault(asset => asset.Name.Equals("SHA256SUMS.txt", StringComparison.Ordinal));
        var bundle = bundleAssets.SingleOrDefault(asset => asset.Name.Equals(expectedBundleFileName, StringComparison.Ordinal));
        if (bundleAssets.Length != 1 || bundle is null || checksumAsset is null)
        {
            AppLogger.Default.Warning("GitHubUpdate", "ReleaseAssetsInvalid", $"Tag={release.TagName}; Bundles={bundleAssets.Length}; Expected={expectedBundleFileName}; Checksum={(checksumAsset is not null)}");
            return new(null, "BundleAssetNotFound");
        }

        ValidateGitHubAssetUri(bundle.DownloadUri);
        ValidateGitHubAssetUri(checksumAsset.DownloadUri);
        var tempRoot = Path.Combine(Path.GetTempPath(), $"UrbanPlanToolbox-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        var checksumPath = Path.Combine(tempRoot, "SHA256SUMS.txt");
        var bundlePath = Path.Combine(tempRoot, bundle.Name);
        try
        {
            progress?.Report(new(AppUpdateState.Downloading, Detail: "Checksum"));
            var checksumText = await _httpClient.GetStringAsync(checksumAsset.DownloadUri, cancellationToken);
            await File.WriteAllTextAsync(checksumPath, checksumText, cancellationToken);
            var expectedHash = ParseChecksum(checksumText, bundle.Name);
            if (expectedHash is null) { LogFailure("ChecksumMissing", release, bundle); return new(null, "ChecksumMissing"); }

            using var response = await _httpClient.GetAsync(bundle.DownloadUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode) { LogFailure("BundleDownloadFailed", release, bundle, response.StatusCode.ToString()); return new(null, "BundleDownloadFailed"); }
            var total = response.Content.Headers.ContentLength;
            long downloaded = 0;
            {
                await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var destination = File.Create(bundlePath);
                var buffer = new byte[1024 * 128];
                int read;
                while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    downloaded += read;
                    progress?.Report(new(AppUpdateState.Downloading, total is > 0 ? (double)downloaded / total.Value : null, $"{downloaded} bytes"));
                }
            }
            if (downloaded <= 0) { LogFailure("BundleDownloadFailed", release, bundle, "Empty file"); return new(null, "BundleDownloadFailed"); }
            await using var bundleStream = File.OpenRead(bundlePath);
            var actualHash = (await SHA256.HashDataAsync(bundleStream, cancellationToken)).ToHexUpper();
            if (!actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase)) { LogFailure("ChecksumMismatch", release, bundle, $"Expected={expectedHash}; Actual={actualHash}"); return new(null, "ChecksumMismatch"); }

            var signature = _signatureVerifier.Verify(bundlePath);
            if (!signature.IsValid)
            {
                LogSignatureFailure(signature.FailureCode, release, bundle, signature);
                return new(null, signature.FailureCode);
            }
            if (!ExpectedSignerSubject.Equals(signature.SignerSubject, StringComparison.Ordinal))
            {
                LogSignatureFailure("SignerSubjectMismatch", release, bundle, signature);
                return new(null, "SignerSubjectMismatch");
            }
            if (!ExpectedSignerThumbprint.Equals(signature.SignerThumbprint, StringComparison.OrdinalIgnoreCase))
            {
                LogSignatureFailure("SignerThumbprintMismatch", release, bundle, signature);
                return new(null, "SignerThumbprintMismatch");
            }
            AppLogger.Default.Info("GitHubUpdate", "BundleSignatureVerified", $"Tag={release.TagName}; Asset={bundle.Name}; Bytes={downloaded}; SHA256={actualHash}; SignerSubject={signature.SignerSubject}; SignerThumbprint={signature.SignerThumbprint}; SignatureStatus={signature.FailureCode}");
            return new(bundlePath);
        }
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException exception) { LogFailure("GitHubNetworkFailure", release, bundle, exception.Message); return new(null, "GitHubNetworkFailure"); }
        catch (IOException exception) { LogFailure("BundleDownloadFailed", release, bundle, exception.Message); return new(null, "BundleDownloadFailed"); }
        catch (JsonException exception) { LogFailure("ChecksumDownloadFailed", release, bundle, exception.Message); return new(null, "ChecksumDownloadFailed"); }
    }

    private static string? ParseChecksum(string content, string fileName) => content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
        .Select(line => line.Trim())
        .Select(line => System.Text.RegularExpressions.Regex.Match(line, $"^(?<hash>[A-Fa-f0-9]{{64}})\\s+\\*?{System.Text.RegularExpressions.Regex.Escape(fileName)}$"))
        .Where(match => match.Success)
        .Select(match => match.Groups["hash"].Value.ToUpperInvariant())
        .SingleOrDefault();

    private static void ValidateGitHubAssetUri(Uri uri)
    {
        if (uri.Scheme != Uri.UriSchemeHttps || !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Release asset is not a GitHub HTTPS URL.");
    }

    private static void LogFailure(string eventName, GitHubRelease release, GitHubReleaseAsset asset, string? detail = null) =>
        AppLogger.Default.Warning("GitHubUpdate", eventName, $"Tag={release.TagName}; Asset={asset.Name}; Host={asset.DownloadUri.Host}; {detail}");

    private static void LogSignatureFailure(string eventName, GitHubRelease release, GitHubReleaseAsset asset, BundleSignatureVerificationResult signature) =>
        AppLogger.Default.Warning("GitHubUpdate", "SignatureVerificationFailed", $"Reason={eventName}; Tag={release.TagName}; Asset={asset.Name}; ExpectedSubject={ExpectedSignerSubject}; ActualSubject={signature.SignerSubject ?? "<none>"}; ExpectedThumbprint={ExpectedSignerThumbprint}; ActualThumbprint={signature.SignerThumbprint ?? "<none>"}; VerificationResult={signature.FailureCode}; HRESULT={(signature.HResult is { } hresult ? $"0x{hresult:X8}" : "<none>")}");

    private static HttpClient CreateClient() => new() { Timeout = TimeSpan.FromSeconds(15) };

    private sealed record ReleasePayload(
        [property: JsonPropertyName("tag_name")] string? TagName,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("body")] string? Body,
        [property: JsonPropertyName("html_url")] string? HtmlUrl,
        [property: JsonPropertyName("published_at")] DateTimeOffset? PublishedAt,
        [property: JsonPropertyName("assets")] ReleaseAssetPayload[]? Assets);

    private sealed record ReleaseAssetPayload(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("browser_download_url")] string? DownloadUrl,
        [property: JsonPropertyName("size")] long? Size,
        [property: JsonPropertyName("digest")] string? Digest);
}

public sealed record BundleDownloadVerificationResult(string? BundlePath, string? FailureCode = null);

file static class HashExtensions
{
    public static string ToHexUpper(this byte[] bytes) => Convert.ToHexString(bytes);
}
