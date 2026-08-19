using System.IO.Compression;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.Services;

public sealed class DataPackInstaller
{
    private const long MaxArchiveBytes = 64L * 1024 * 1024;
    private const long MaxExpandedBytes = 128L * 1024 * 1024;
    private const int MaxEntries = 64;
    private const string OfficialReleasePrefix = "/KiYouJyo/UrbanPlanToolbox_Data/releases/download/";
    private const string ReleaseApiPrefix = "https://api.github.com/repos/KiYouJyo/UrbanPlanToolbox_Data/releases/tags/";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    private readonly DataPackStateStore _stateStore;
    private readonly HttpClient _http;

    internal DataPackInstaller(DataPackStateStore stateStore, HttpClient httpClient)
    {
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<ReferenceDataPackState> InstallFromFileAsync(string packId, string sourcePath, string sourceKind = "local", CancellationToken cancellationToken = default)
    {
        ReferenceDataPackService.ValidatePackId(packId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        var source = Path.GetFullPath(sourcePath);
        if (!File.Exists(source)) throw new FileNotFoundException("The selected .uptdata file does not exist.", source);
        var fileInfo = new FileInfo(source);
        if (fileInfo.Length <= 0 || fileInfo.Length > MaxArchiveBytes) throw new InvalidDataException("The .uptdata archive is empty or exceeds the supported size limit.");

        var validated = await ValidateArchiveAsync(packId, source, cancellationToken).ConfigureAwait(false);
        var archiveHash = ComputeSha256(source);
        var directory = _stateStore.GetPackDirectory(packId);
        var archiveFileName = SanitizeArchiveFileName($"{packId}-{validated.Manifest.Version}-{archiveHash[..8].ToLowerInvariant()}.uptdata");
        var destination = Path.Combine(directory, archiveFileName);
        var temp = destination + ".tmp";
        try
        {
            File.Copy(source, temp, true);
            File.Move(temp, destination, true);
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
        }

        var state = new ReferenceDataPackState
        {
            PackId = packId,
            Version = validated.Manifest.Version,
            SchemaVersion = validated.Manifest.SchemaVersion,
            ArchiveFileName = archiveFileName,
            SourceKind = string.IsNullOrWhiteSpace(sourceKind) ? "local" : sourceKind,
            InstalledAt = DateTimeOffset.UtcNow
        };
        await _stateStore.WriteAsync(packId, state, cancellationToken).ConfigureAwait(false);
        AppLogger.Default.Info(nameof(DataPackInstaller), "pack_activated", $"{packId}@{state.Version} ({state.SourceKind})");
        return state;
    }

    public async Task<ReferenceDataPackState> DownloadAndInstallAsync(string packId, ReferenceDataPackCatalogEntry entry, CancellationToken cancellationToken = default)
    {
        ReferenceDataPackService.ValidatePackId(packId);
        if (!string.Equals(entry.PackId, packId, StringComparison.Ordinal)) throw new InvalidDataException("Catalog pack ID does not match the requested data pack.");
        if (entry.SchemaVersion != ReferenceDataPackService.SupportedSchemaVersion) throw new InvalidDataException("The catalog data schema is not supported.");
        if (!Uri.TryCreate(entry.DownloadUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The catalog download URL is invalid.");
        if (!uri.AbsolutePath.Contains(OfficialReleasePrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The catalog download URL does not target the official data repository.");
        if (string.IsNullOrWhiteSpace(entry.Sha256)) throw new InvalidDataException("The catalog does not contain SHA-256 for this data pack.");

        var directory = _stateStore.GetPackDirectory(packId);
        var downloadPath = Path.Combine(directory, $".{packId}-{Guid.NewGuid():N}.download");
        try
        {
            await DownloadVerifiedArchiveAsync(uri, entry, downloadPath, cancellationToken).ConfigureAwait(false);

            // Verify the runtime manifest against the catalog before changing active-pack.json.
            // InstallFromFileAsync validates again immediately before activation; the duplicate pass is
            // intentional so a catalog/version mismatch can never leave a newly activated bad state.
            var validated = await ValidateArchiveAsync(packId, downloadPath, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(validated.Manifest.Version, entry.Version, StringComparison.Ordinal))
                throw new InvalidDataException("The downloaded data-pack version does not match the catalog.");
            if (validated.Manifest.SchemaVersion != entry.SchemaVersion)
                throw new InvalidDataException("The downloaded data-pack schema does not match the catalog.");

            return await InstallFromFileAsync(packId, downloadPath, "official", cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            try { if (File.Exists(downloadPath)) File.Delete(downloadPath); } catch { }
        }
    }

    internal async Task<DataPackValidationResult> ValidateArchiveAsync(string packId, string archivePath, CancellationToken cancellationToken = default)
    {
        ReferenceDataPackService.ValidatePackId(packId);
        cancellationToken.ThrowIfCancellationRequested();
        using var archive = ZipFile.OpenRead(archivePath);
        if (archive.Entries.Count == 0 || archive.Entries.Count > MaxEntries) throw new InvalidDataException("The .uptdata archive has an invalid number of entries.");

        long expandedBytes = 0;
        foreach (var entry in archive.Entries)
        {
            ValidateArchivePath(entry.FullName);
            expandedBytes = checked(expandedBytes + entry.Length);
            if (expandedBytes > MaxExpandedBytes) throw new InvalidDataException("The .uptdata archive expands beyond the supported size limit.");
        }

        var manifestEntry = archive.GetEntry("manifest.json") ?? throw new InvalidDataException("The .uptdata archive does not contain manifest.json.");
        ReferenceDataPackManifest manifest;
        await using (var stream = manifestEntry.Open())
            manifest = await JsonSerializer.DeserializeAsync<ReferenceDataPackManifest>(stream, JsonOptions, cancellationToken).ConfigureAwait(false) ?? throw new InvalidDataException("The data-pack manifest is empty.");
        ValidateManifest(packId, manifest);

        var declaredPaths = new HashSet<string>(StringComparer.Ordinal);
        ReferenceDataPackFile? primaryDataFile = null;
        foreach (var file in manifest.Files)
        {
            var normalizedPath = file.Path.Replace('\\', '/');
            ValidateArchivePath(normalizedPath);
            if (!declaredPaths.Add(normalizedPath)) throw new InvalidDataException("The data-pack manifest contains duplicate file paths.");
            var payloadEntry = archive.GetEntry(normalizedPath) ?? throw new InvalidDataException($"The .uptdata archive does not contain {normalizedPath}.");
            if (file.Size < 0 || payloadEntry.Length != file.Size) throw new InvalidDataException($"The payload size for {normalizedPath} does not match manifest.json.");
            await VerifyEntrySha256Async(payloadEntry, file.Sha256, cancellationToken).ConfigureAwait(false);
            if (normalizedPath.StartsWith("data/", StringComparison.Ordinal) && normalizedPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                if (primaryDataFile is not null) throw new InvalidDataException("Data Pack 1.0 requires exactly one primary data JSON file.");
                primaryDataFile = file;
            }
        }

        foreach (var entry in archive.Entries.Where(item => item.Length > 0))
        {
            var normalized = entry.FullName.Replace('\\', '/');
            if (!string.Equals(normalized, "manifest.json", StringComparison.Ordinal) && !declaredPaths.Contains(normalized))
                throw new InvalidDataException("The .uptdata archive contains an undeclared payload file.");
        }
        if (primaryDataFile is null) throw new InvalidDataException("The data-pack manifest does not declare a primary data JSON file.");

        var dataEntry = archive.GetEntry(primaryDataFile.Path.Replace('\\', '/'))!;
        string dataJson;
        using (var reader = new StreamReader(dataEntry.Open())) dataJson = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        ReferenceDataPackService.ValidateFeatureData(packId, dataJson);
        return new DataPackValidationResult(manifest, dataJson);
    }

    private async Task DownloadVerifiedArchiveAsync(Uri primaryUri, ReferenceDataPackCatalogEntry entry, string downloadPath, CancellationToken cancellationToken)
    {
        var failures = new List<Exception>();

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                await DownloadOnceAsync(primaryUri, downloadPath, null, cancellationToken).ConfigureAwait(false);
                VerifyCatalogDownload(downloadPath, entry);
                if (attempt > 1)
                    AppLogger.Default.Info(nameof(DataPackInstaller), "pack_download_retry_succeeded", $"{entry.PackId}@{entry.Version}; attempt={attempt}");
                return;
            }
            catch (Exception exception) when (IsRetryableDownloadFailure(exception, cancellationToken))
            {
                failures.Add(exception);
                AppLogger.Default.Warning(nameof(DataPackInstaller), "pack_download_attempt_failed", $"{entry.PackId}@{entry.Version}; direct attempt={attempt}; {exception.Message}");
                TryDelete(downloadPath);
                if (attempt < 2) await Task.Delay(TimeSpan.FromMilliseconds(350 * attempt), cancellationToken).ConfigureAwait(false);
            }
        }

        try
        {
            var assetApiUri = await ResolveReleaseAssetApiUriAsync(primaryUri, entry.FileName, cancellationToken).ConfigureAwait(false);
            await DownloadOnceAsync(assetApiUri, downloadPath, "application/octet-stream", cancellationToken).ConfigureAwait(false);
            VerifyCatalogDownload(downloadPath, entry);
            AppLogger.Default.Info(nameof(DataPackInstaller), "pack_download_api_fallback_succeeded", $"{entry.PackId}@{entry.Version}; asset={entry.FileName}");
            return;
        }
        catch (Exception exception) when (IsRetryableDownloadFailure(exception, cancellationToken))
        {
            failures.Add(exception);
            TryDelete(downloadPath);
            AppLogger.Default.Warning(nameof(DataPackInstaller), "pack_download_api_fallback_failed", $"{entry.PackId}@{entry.Version}; {exception.Message}");
        }

        throw new HttpRequestException(
            $"Unable to download and verify the official data pack after direct retries and the GitHub API fallback. {string.Join(" | ", failures.Select(item => item.Message))}",
            failures.LastOrDefault());
    }

    private async Task DownloadOnceAsync(Uri uri, string downloadPath, string? accept, CancellationToken cancellationToken)
    {
        TryDelete(downloadPath);
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        if (!string.IsNullOrWhiteSpace(accept)) request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));
        request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");

        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is > MaxArchiveBytes) throw new InvalidDataException("The remote data pack exceeds the supported size limit.");
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var output = new FileStream(downloadPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
        await CopyWithLimitAsync(input, output, MaxArchiveBytes, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Uri> ResolveReleaseAssetApiUriAsync(Uri browserDownloadUri, string expectedFileName, CancellationToken cancellationToken)
    {
        var path = browserDownloadUri.AbsolutePath;
        var prefixIndex = path.IndexOf(OfficialReleasePrefix, StringComparison.OrdinalIgnoreCase);
        if (prefixIndex < 0) throw new InvalidDataException("The official release URL cannot be resolved to the GitHub API.");
        var remainder = path[(prefixIndex + OfficialReleasePrefix.Length)..];
        var separator = remainder.IndexOf('/');
        if (separator <= 0 || separator >= remainder.Length - 1) throw new InvalidDataException("The official release URL is missing a tag or asset name.");
        var tag = Uri.UnescapeDataString(remainder[..separator]);
        var fileName = Uri.UnescapeDataString(remainder[(separator + 1)..]);
        if (!string.IsNullOrWhiteSpace(expectedFileName) && !string.Equals(fileName, expectedFileName, StringComparison.Ordinal))
            throw new InvalidDataException("The catalog file name does not match the release asset URL.");

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(ReleaseApiPrefix + Uri.EscapeDataString(tag)));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var release = await JsonSerializer.DeserializeAsync<GitHubReleaseAssetEnvelope>(stream, JsonOptions, cancellationToken).ConfigureAwait(false)
                      ?? throw new InvalidDataException("The GitHub release metadata is empty.");
        var asset = release.Assets.FirstOrDefault(candidate => string.Equals(candidate.Name, fileName, StringComparison.Ordinal));
        if (asset is null || !Uri.TryCreate(asset.Url, UriKind.Absolute, out var apiUri) || apiUri.Scheme != Uri.UriSchemeHttps || !string.Equals(apiUri.Host, "api.github.com", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The matching official data-pack asset could not be resolved through the GitHub API.");
        return apiUri;
    }

    private static bool IsRetryableDownloadFailure(Exception exception, CancellationToken cancellationToken) =>
        exception is HttpRequestException or IOException or InvalidDataException ||
        exception is OperationCanceledException && !cancellationToken.IsCancellationRequested;

    private static void VerifyCatalogDownload(string downloadPath, ReferenceDataPackCatalogEntry entry)
    {
        var downloadedLength = new FileInfo(downloadPath).Length;
        if (downloadedLength <= 0) throw new InvalidDataException("The downloaded data pack is empty.");
        if (entry.SizeBytes is > 0 && downloadedLength != entry.SizeBytes.Value) throw new InvalidDataException("The downloaded data-pack size does not match the catalog.");
        VerifySha256(downloadPath, entry.Sha256);
    }

    private static void ValidateManifest(string packId, ReferenceDataPackManifest manifest)
    {
        if (manifest.FormatVersion != ReferenceDataPackService.SupportedFormatVersion) throw new InvalidDataException("Unsupported .uptdata format version.");
        if (!string.Equals(manifest.Id, packId, StringComparison.Ordinal)) throw new InvalidDataException("The .uptdata pack ID does not match this library.");
        if (manifest.SchemaVersion != ReferenceDataPackService.SupportedSchemaVersion) throw new InvalidDataException("Unsupported data schema version.");
        if (!string.Equals(manifest.Publisher, "UrbanPlanToolbox", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The data-pack publisher is not trusted.");
        if (manifest.Channel is not ("stable" or "preview")) throw new InvalidDataException("The data-pack channel is invalid.");
        if (ReferenceDataPackService.ParseDataVersion(manifest.Version) == ReferenceDataPackService.DataPackVersion.Zero) throw new InvalidDataException("The data-pack version is invalid.");
        if (manifest.DisplayName.Count == 0 || !new[] { "zh-CN", "ja-JP", "en-US" }.All(manifest.DisplayName.ContainsKey)) throw new InvalidDataException("The data-pack manifest is missing trilingual display names.");
        if (manifest.Files.Count is < 1 or > 32) throw new InvalidDataException("The data-pack manifest has an invalid files list.");
        if (!string.IsNullOrWhiteSpace(manifest.MinAppVersion) && VersionParser.TryParseTag(manifest.MinAppVersion, out var minimum) && AppVersionProvider.GetCurrentVersion().CompareTo(minimum) < 0)
            throw new InvalidDataException($"This data pack requires UrbanPlanToolbox {manifest.MinAppVersion} or later.");
    }

    private static async Task VerifyEntrySha256Async(ZipArchiveEntry entry, string expected, CancellationToken cancellationToken)
    {
        if (!IsSha256(expected)) throw new InvalidDataException($"The manifest SHA-256 for {entry.FullName} is invalid.");
        await using var stream = entry.Open();
        using var sha = SHA256.Create();
        var actual = await sha.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
        var expectedBytes = Convert.FromHexString(expected);
        if (!CryptographicOperations.FixedTimeEquals(actual, expectedBytes)) throw new InvalidDataException($"The payload SHA-256 for {entry.FullName} does not match manifest.json.");
    }

    private static void VerifySha256(string path, string expected)
    {
        if (!IsSha256(expected)) throw new InvalidDataException("The catalog SHA-256 is invalid.");
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        var actual = sha.ComputeHash(stream);
        var expectedBytes = Convert.FromHexString(expected);
        if (!CryptographicOperations.FixedTimeEquals(actual, expectedBytes)) throw new InvalidDataException("The downloaded data pack failed SHA-256 verification.");
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(stream));
    }

    private static bool IsSha256(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private static async Task CopyWithLimitAsync(Stream input, Stream output, long maxBytes, CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long total = 0;
        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            total += read;
            if (total > maxBytes) throw new InvalidDataException("The downloaded data pack exceeds the supported size limit.");
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }
    }

    private static void ValidateArchivePath(string path)
    {
        var normalized = path.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(normalized) || normalized.StartsWith("/", StringComparison.Ordinal) || normalized.Contains(':')) throw new InvalidDataException("The .uptdata archive contains an unsafe path.");
        if (normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(segment => segment is "." or "..")) throw new InvalidDataException("The .uptdata archive contains path traversal.");
    }

    private static string SanitizeArchiveFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var value = string.Concat(fileName.Select(character => invalid.Contains(character) ? '_' : character));
        return value.EndsWith(".uptdata", StringComparison.OrdinalIgnoreCase) ? value : value + ".uptdata";
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    private sealed class GitHubReleaseAssetEnvelope
    {
        public List<GitHubReleaseAsset> Assets { get; init; } = [];
    }

    private sealed class GitHubReleaseAsset
    {
        public string Name { get; init; } = string.Empty;
        public string Url { get; init; } = string.Empty;
    }
}

internal sealed record DataPackValidationResult(ReferenceDataPackManifest Manifest, string DataJson);