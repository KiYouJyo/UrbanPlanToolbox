using System.Net.Http.Headers;
using System.Text.Json;
using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.Services;

public sealed class DataPackCatalogService
{
    public const int SupportedCatalogVersion = 1;
    public const string CatalogUrl = "https://raw.githubusercontent.com/KiYouJyo/UrbanPlanToolbox_Data/main/catalog/catalog-v1.json";
    public const string CatalogApiUrl = "https://api.github.com/repos/KiYouJyo/UrbanPlanToolbox_Data/contents/catalog/catalog-v1.json?ref=main";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
    private static readonly CatalogEndpoint[] Endpoints =
    [
        new("raw", CatalogUrl, null),
        new("api", CatalogApiUrl, "application/vnd.github.raw+json")
    ];

    private readonly HttpClient _http;

    public DataPackCatalogService(HttpClient httpClient) => _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

    public async Task<ReferenceDataPackCatalogEntry?> GetLatestAsync(string packId, CancellationToken cancellationToken = default)
    {
        ReferenceDataPackService.ValidatePackId(packId);
        var catalog = await FetchCatalogAsync(cancellationToken).ConfigureAwait(false);

        var item = catalog.Packs
            .Where(candidate => string.Equals(candidate.Id, packId, StringComparison.Ordinal))
            .OrderByDescending(candidate => ReferenceDataPackService.ParseDataVersion(candidate.Version))
            .FirstOrDefault();
        if (item is null) return null;
        if (item.SchemaVersion != ReferenceDataPackService.SupportedSchemaVersion) return null;
        if (!Uri.TryCreate(item.DownloadUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps) throw new InvalidDataException("The catalog contains an invalid data-pack URL.");
        var fileName = Uri.UnescapeDataString(uri.Segments.LastOrDefault() ?? string.Empty).Trim('/');
        return new ReferenceDataPackCatalogEntry(item.Id, item.Version, item.SchemaVersion, item.MinAppVersion, item.DownloadUrl, item.Sha256, fileName, item.Size);
    }

    public async Task<ReferenceDataPackUpdateInfo> CheckForUpdateAsync(string packId, ReferenceDataPackState? local, CancellationToken cancellationToken = default)
    {
        try
        {
            var remote = await GetLatestAsync(packId, cancellationToken).ConfigureAwait(false);
            if (remote is null) return new(null, local, false, "catalog-missing-pack");
            if (!string.IsNullOrWhiteSpace(remote.MinAppVersion) && VersionParser.TryParseTag(remote.MinAppVersion, out var minimum) && AppVersionProvider.GetCurrentVersion().CompareTo(minimum) < 0)
                return new(remote, local, false, "requires-newer-app");
            var updateAvailable = local is null || ReferenceDataPackService.ParseDataVersion(remote.Version).CompareTo(ReferenceDataPackService.ParseDataVersion(local.Version)) > 0;
            return new(remote, local, updateAvailable, updateAvailable ? "update-available" : "latest");
        }
        catch (CatalogAccessException exception)
        {
            AppLogger.Default.Warning(nameof(DataPackCatalogService), "catalog_check_failed", $"Status={exception.Status}; {exception.Message}");
            return new(null, local, false, exception.Status);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            AppLogger.Default.Warning(nameof(DataPackCatalogService), "catalog_check_failed", "Status=catalog-timeout; catalog request timed out.");
            return new(null, local, false, "catalog-timeout");
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidDataException)
        {
            AppLogger.Default.Warning(nameof(DataPackCatalogService), "catalog_check_failed", exception.Message);
            return new(null, local, false, exception is JsonException or InvalidDataException ? "catalog-invalid" : "catalog-network-unavailable");
        }
    }

    private async Task<ReferenceDataPackCatalog> FetchCatalogAsync(CancellationToken cancellationToken)
    {
        var failures = new List<string>();
        var sawInvalidPayload = false;

        for (var index = 0; index < Endpoints.Length; index++)
        {
            var endpoint = Endpoints[index];
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, endpoint.Url);
                if (endpoint.Accept is not null) request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(endpoint.Accept));
                request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");

                using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    failures.Add($"{endpoint.Name}: HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
                    continue;
                }

                var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var catalog = JsonSerializer.Deserialize<ReferenceDataPackCatalog>(json, JsonOptions) ?? throw new InvalidDataException("The data-pack catalog is empty.");
                if (catalog.CatalogVersion != SupportedCatalogVersion) throw new InvalidDataException($"Unsupported data-pack catalog version {catalog.CatalogVersion}.");
                if (catalog.Packs.Count == 0) throw new InvalidDataException("The data-pack catalog contains no packs.");

                if (index > 0)
                    AppLogger.Default.Info(nameof(DataPackCatalogService), "catalog_fallback_used", $"Endpoint={endpoint.Name}; Url={endpoint.Url}");
                return catalog;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                failures.Add($"{endpoint.Name}: timeout");
            }
            catch (HttpRequestException exception)
            {
                failures.Add($"{endpoint.Name}: {exception.Message}");
            }
            catch (JsonException exception)
            {
                sawInvalidPayload = true;
                failures.Add($"{endpoint.Name}: invalid JSON ({exception.Message})");
            }
            catch (InvalidDataException exception)
            {
                sawInvalidPayload = true;
                failures.Add($"{endpoint.Name}: invalid catalog ({exception.Message})");
            }
        }

        var status = sawInvalidPayload ? "catalog-invalid" : failures.Any(failure => failure.Contains("timeout", StringComparison.OrdinalIgnoreCase)) ? "catalog-timeout" : "catalog-network-unavailable";
        throw new CatalogAccessException(status, $"All catalog endpoints failed. {string.Join(" | ", failures)}");
    }

    private sealed record CatalogEndpoint(string Name, string Url, string? Accept);

    private sealed class CatalogAccessException : Exception
    {
        public CatalogAccessException(string status, string message) : base(message) => Status = status;
        public string Status { get; }
    }
}
