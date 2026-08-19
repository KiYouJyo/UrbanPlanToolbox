using System.Text.Json;
using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.Services;

public sealed class DataPackCatalogService
{
    public const int SupportedCatalogVersion = 1;
    public const string CatalogUrl = "https://raw.githubusercontent.com/KiYouJyo/UrbanPlanToolbox_Data/main/catalog/catalog-v1.json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
    private readonly HttpClient _http;

    public DataPackCatalogService(HttpClient httpClient) => _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

    public async Task<ReferenceDataPackCatalogEntry?> GetLatestAsync(string packId, CancellationToken cancellationToken = default)
    {
        ReferenceDataPackService.ValidatePackId(packId);
        using var response = await _http.GetAsync(CatalogUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return null;
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var catalog = JsonSerializer.Deserialize<ReferenceDataPackCatalog>(json, JsonOptions) ?? throw new InvalidDataException("The data-pack catalog is empty.");
        if (catalog.CatalogVersion != SupportedCatalogVersion) throw new InvalidDataException("Unsupported data-pack catalog version.");

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
            if (remote is null) return new(null, local, false, "catalog-unavailable");
            if (!string.IsNullOrWhiteSpace(remote.MinAppVersion) && VersionParser.TryParseTag(remote.MinAppVersion, out var minimum) && AppVersionProvider.GetCurrentVersion().CompareTo(minimum) < 0)
                return new(remote, local, false, "requires-newer-app");
            var updateAvailable = local is null || ReferenceDataPackService.ParseDataVersion(remote.Version).CompareTo(ReferenceDataPackService.ParseDataVersion(local.Version)) > 0;
            return new(remote, local, updateAvailable, updateAvailable ? "update-available" : "latest");
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or InvalidDataException)
        {
            AppLogger.Default.Warning(nameof(DataPackCatalogService), "catalog_check_failed", exception.Message);
            return new(null, local, false, "catalog-unavailable");
        }
    }
}
