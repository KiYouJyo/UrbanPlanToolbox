using System.Net;
using System.Net.Http.Headers;
using System.Text;
using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.Services;

public interface IWebDavClient
{
    Task<WebDavResult> TestConnectionAsync(WebDavProfile profile, string password, CancellationToken cancellationToken = default);
    Task<WebDavResult> UploadAsync(WebDavProfile profile, string password, string localPath, string fileName, CancellationToken cancellationToken = default);
    Task<WebDavListResult> ListAsync(WebDavProfile profile, string password, CancellationToken cancellationToken = default);
    Task<WebDavResult> DownloadAsync(WebDavProfile profile, string password, string fileName, string destinationPath, CancellationToken cancellationToken = default);
    Task<WebDavResult> DeleteAsync(WebDavProfile profile, string password, string fileName, CancellationToken cancellationToken = default);
}

public sealed class WebDavClient : IWebDavClient
{
    private static readonly HttpMethod PropFindMethod = new("PROPFIND");
    private static readonly HttpMethod MkColMethod = new("MKCOL");
    private readonly HttpClient _httpClient;

    public WebDavClient(HttpMessageHandler? handler = null)
    {
        _httpClient = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<WebDavResult> TestConnectionAsync(WebDavProfile profile, string password, CancellationToken cancellationToken = default)
    {
        if (!WebDavProfileService.TryNormalize(profile, out var normalized, out var errorCode)) return new(WebDavStatus.InvalidConfiguration, errorCode);
        var ensure = await EnsureCollectionAsync(normalized, password, cancellationToken).ConfigureAwait(false);
        if (!ensure.Succeeded) return ensure;
        var probeName = $".urbanplantoolbox-{Guid.NewGuid():N}.probe";
        var probeUri = BuildFileUri(normalized, probeName);
        var put = await PutBytesAsync(normalized, password, probeUri, Encoding.UTF8.GetBytes("UrbanPlanToolbox"), cancellationToken).ConfigureAwait(false);
        if (!put.Succeeded) return put;
        var delete = await SendNoContentAsync(normalized, password, HttpMethod.Delete, probeUri, cancellationToken).ConfigureAwait(false);
        return delete.Status == WebDavStatus.NotFound ? new(WebDavStatus.Success) : delete;
    }

    public async Task<WebDavResult> UploadAsync(WebDavProfile profile, string password, string localPath, string fileName, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(localPath)) return new(WebDavStatus.IoFailure, "LocalFileMissing");
        if (!WebDavDirectoryListingParser.IsBackupFileName(fileName)) return new(WebDavStatus.InvalidConfiguration, "FileNameInvalid");
        if (!WebDavProfileService.TryNormalize(profile, out var normalized, out var errorCode)) return new(WebDavStatus.InvalidConfiguration, errorCode);
        var ensure = await EnsureCollectionAsync(normalized, password, cancellationToken).ConfigureAwait(false);
        if (!ensure.Succeeded) return ensure;

        var finalUri = BuildFileUri(normalized, fileName);
        var expectedLength = new FileInfo(localPath).Length;
        var put = await PutFileAsync(normalized, password, finalUri, localPath, cancellationToken).ConfigureAwait(false);
        if (!put.Succeeded) return put;

        return await VerifyUploadedFileAsync(normalized, password, finalUri, fileName, expectedLength, cancellationToken).ConfigureAwait(false);
    }

    public async Task<WebDavListResult> ListAsync(WebDavProfile profile, string password, CancellationToken cancellationToken = default)
    {
        if (!WebDavProfileService.TryNormalize(profile, out var normalized, out var errorCode)) return new(WebDavStatus.InvalidConfiguration, [], errorCode);
        try
        {
            var collectionUri = BuildCollectionUri(normalized);
            using var request = CreateRequest(normalized, password, PropFindMethod, collectionUri);
            request.Headers.TryAddWithoutValidation("Depth", "1");
            request.Content = CreatePropFindContent();
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if ((int)response.StatusCode != 207)
            {
                var failure = FromStatusCode(response.StatusCode, "PropFindFailed");
                return new(failure.Status, [], failure.ErrorCode);
            }

            var xml = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var items = WebDavDirectoryListingParser.Parse(xml);
            return new(WebDavStatus.Success, items);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { return new(WebDavStatus.Timeout, [], "PropFindTimeout"); }
        catch (HttpRequestException) { return new(WebDavStatus.TransportFailure, [], "PropFindTransportFailure"); }
        catch (Exception exception) when (exception is System.Xml.XmlException or FormatException) { return new(WebDavStatus.ProtocolFailure, [], "PropFindInvalidResponse"); }
    }

    public async Task<WebDavResult> DownloadAsync(WebDavProfile profile, string password, string fileName, string destinationPath, CancellationToken cancellationToken = default)
    {
        if (!WebDavDirectoryListingParser.IsBackupFileName(fileName)) return new(WebDavStatus.InvalidConfiguration, "FileNameInvalid");
        if (!WebDavProfileService.TryNormalize(profile, out var normalized, out var errorCode)) return new(WebDavStatus.InvalidConfiguration, errorCode);
        var temporaryPath = destinationPath + ".download";
        try
        {
            using var request = CreateRequest(normalized, password, HttpMethod.Get, BuildFileUri(normalized, fileName));
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return FromStatusCode(response.StatusCode, "DownloadFailed");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destinationPath))!);
            await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            await using (var target = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                await source.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
            File.Move(temporaryPath, destinationPath, overwrite: true);
            return new(WebDavStatus.Success);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { return new(WebDavStatus.Timeout, "DownloadTimeout"); }
        catch (HttpRequestException) { return new(WebDavStatus.TransportFailure, "DownloadTransportFailure"); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return new(WebDavStatus.IoFailure, exception.GetType().Name); }
        finally { TryDelete(temporaryPath); }
    }

    public async Task<WebDavResult> DeleteAsync(WebDavProfile profile, string password, string fileName, CancellationToken cancellationToken = default)
    {
        if (!WebDavDirectoryListingParser.IsBackupFileName(fileName)) return new(WebDavStatus.InvalidConfiguration, "FileNameInvalid");
        if (!WebDavProfileService.TryNormalize(profile, out var normalized, out var errorCode)) return new(WebDavStatus.InvalidConfiguration, errorCode);
        return await SendNoContentAsync(normalized, password, HttpMethod.Delete, BuildFileUri(normalized, fileName), cancellationToken).ConfigureAwait(false);
    }

    private async Task<WebDavResult> VerifyUploadedFileAsync(
        WebDavProfile profile,
        string password,
        Uri fileUri,
        string fileName,
        long expectedLength,
        CancellationToken cancellationToken)
    {
        WebDavResult lastResult = new(WebDavStatus.ProtocolFailure, "UploadVerificationFailed");
        var delays = new[] { 0, 300, 900, 1800 };

        foreach (var delayMs in delays)
        {
            if (delayMs > 0) await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);

            var head = await VerifyWithHeadAsync(profile, password, fileUri, expectedLength, cancellationToken).ConfigureAwait(false);
            if (head.Succeeded) return head;
            lastResult = head;

            var propFind = await VerifyWithPropFindAsync(profile, password, fileUri, fileName, expectedLength, cancellationToken).ConfigureAwait(false);
            if (propFind.Succeeded) return propFind;
            lastResult = propFind;
        }

        return lastResult;
    }

    private async Task<WebDavResult> VerifyWithHeadAsync(
        WebDavProfile profile,
        string password,
        Uri fileUri,
        long expectedLength,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = CreateRequest(profile, password, HttpMethod.Head, fileUri);
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return FromStatusCode(response.StatusCode, "UploadHeadVerificationFailed");

            var remoteLength = response.Content.Headers.ContentLength;
            if (remoteLength.HasValue)
                return remoteLength.Value == expectedLength
                    ? new(WebDavStatus.Success)
                    : new(WebDavStatus.ProtocolFailure, "UploadSizeMismatch");

            return new(WebDavStatus.ProtocolFailure, "UploadLengthUnavailable");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { return new(WebDavStatus.Timeout, "UploadHeadVerificationTimeout"); }
        catch (HttpRequestException) { return new(WebDavStatus.TransportFailure, "UploadHeadVerificationTransportFailure"); }
    }

    private async Task<WebDavResult> VerifyWithPropFindAsync(
        WebDavProfile profile,
        string password,
        Uri fileUri,
        string fileName,
        long expectedLength,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = CreateRequest(profile, password, PropFindMethod, fileUri);
            request.Headers.TryAddWithoutValidation("Depth", "0");
            request.Content = CreatePropFindContent();
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if ((int)response.StatusCode != 207) return FromStatusCode(response.StatusCode, "UploadPropFindVerificationFailed");

            var xml = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var item = WebDavDirectoryListingParser.Parse(xml)
                .FirstOrDefault(candidate => string.Equals(candidate.FileName, fileName, StringComparison.OrdinalIgnoreCase));
            if (item is null) return new(WebDavStatus.NotFound, "UploadNotVisibleAfterPut");
            return item.Size == expectedLength
                ? new(WebDavStatus.Success)
                : new(WebDavStatus.ProtocolFailure, "UploadSizeMismatch");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { return new(WebDavStatus.Timeout, "UploadPropFindVerificationTimeout"); }
        catch (HttpRequestException) { return new(WebDavStatus.TransportFailure, "UploadPropFindVerificationTransportFailure"); }
        catch (Exception exception) when (exception is System.Xml.XmlException or FormatException) { return new(WebDavStatus.ProtocolFailure, "UploadPropFindInvalidResponse"); }
    }

    private async Task<WebDavResult> EnsureCollectionAsync(WebDavProfile profile, string password, CancellationToken cancellationToken)
    {
        var baseUri = new Uri(profile.ServerUrl, UriKind.Absolute);
        var relative = string.Empty;
        foreach (var segment in profile.RemotePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            relative += Uri.EscapeDataString(segment) + "/";
            var uri = new Uri(baseUri, relative);
            try
            {
                using var request = CreateRequest(profile, password, MkColMethod, uri);
                using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if (response.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK or HttpStatusCode.NoContent or HttpStatusCode.MethodNotAllowed) continue;
                return FromStatusCode(response.StatusCode, "CreateCollectionFailed");
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { return new(WebDavStatus.Timeout, "CreateCollectionTimeout"); }
            catch (HttpRequestException) { return new(WebDavStatus.TransportFailure, "CreateCollectionTransportFailure"); }
        }
        return new(WebDavStatus.Success);
    }

    private async Task<WebDavResult> PutFileAsync(WebDavProfile profile, string password, Uri uri, string localPath, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
            using var request = CreateRequest(profile, password, HttpMethod.Put, uri);
            request.Content = new StreamContent(stream);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            request.Content.Headers.ContentLength = stream.Length;
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode ? new(WebDavStatus.Success) : FromStatusCode(response.StatusCode, "UploadFailed");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { return new(WebDavStatus.Timeout, "UploadTimeout"); }
        catch (HttpRequestException) { return new(WebDavStatus.TransportFailure, "UploadTransportFailure"); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return new(WebDavStatus.IoFailure, exception.GetType().Name); }
    }

    private async Task<WebDavResult> PutBytesAsync(WebDavProfile profile, string password, Uri uri, byte[] payload, CancellationToken cancellationToken)
    {
        try
        {
            using var request = CreateRequest(profile, password, HttpMethod.Put, uri);
            request.Content = new ByteArrayContent(payload);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            request.Content.Headers.ContentLength = payload.LongLength;
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode ? new(WebDavStatus.Success) : FromStatusCode(response.StatusCode, "ProbeUploadFailed");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { return new(WebDavStatus.Timeout, "ProbeTimeout"); }
        catch (HttpRequestException) { return new(WebDavStatus.TransportFailure, "ProbeTransportFailure"); }
    }

    private async Task<WebDavResult> SendNoContentAsync(WebDavProfile profile, string password, HttpMethod method, Uri uri, CancellationToken cancellationToken)
    {
        try
        {
            using var request = CreateRequest(profile, password, method, uri);
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode ? new(WebDavStatus.Success) : FromStatusCode(response.StatusCode, "RequestFailed");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { return new(WebDavStatus.Timeout, "RequestTimeout"); }
        catch (HttpRequestException) { return new(WebDavStatus.TransportFailure, "RequestTransportFailure"); }
    }

    private static HttpRequestMessage CreateRequest(WebDavProfile profile, string password, HttpMethod method, Uri uri)
    {
        var request = new HttpRequestMessage(method, uri);
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{profile.Username}:{password}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
        request.Headers.UserAgent.ParseAdd($"UrbanPlanToolbox/{AppVersionProvider.Version}");
        return request;
    }

    private static StringContent CreatePropFindContent() => new(
        "<?xml version=\"1.0\" encoding=\"utf-8\"?><d:propfind xmlns:d=\"DAV:\"><d:prop><d:displayname/><d:getcontentlength/><d:getlastmodified/><d:resourcetype/></d:prop></d:propfind>",
        Encoding.UTF8,
        "application/xml");

    private static Uri BuildCollectionUri(WebDavProfile profile)
    {
        var baseUri = new Uri(profile.ServerUrl, UriKind.Absolute);
        var relative = string.Join('/', profile.RemotePath.Trim('/').Split('/').Select(Uri.EscapeDataString)) + "/";
        return new Uri(baseUri, relative);
    }

    private static Uri BuildFileUri(WebDavProfile profile, string fileName) => new(BuildCollectionUri(profile), Uri.EscapeDataString(fileName));

    private static WebDavResult FromStatusCode(HttpStatusCode statusCode, string fallbackCode) => statusCode switch
    {
        HttpStatusCode.Unauthorized => new(WebDavStatus.AuthenticationFailed, "Unauthorized"),
        HttpStatusCode.Forbidden => new(WebDavStatus.Forbidden, "Forbidden"),
        HttpStatusCode.NotFound => new(WebDavStatus.NotFound, "NotFound"),
        HttpStatusCode.Conflict or HttpStatusCode.PreconditionFailed => new(WebDavStatus.Conflict, "Conflict"),
        _ when (int)statusCode >= 500 => new(WebDavStatus.ServerFailure, $"Http{(int)statusCode}"),
        _ => new(WebDavStatus.ProtocolFailure, fallbackCode)
    };

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
    }
}
