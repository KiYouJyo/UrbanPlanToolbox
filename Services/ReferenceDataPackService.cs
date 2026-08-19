using System.Text.Json;
using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.Services;

public sealed class ReferenceDataPackService
{
    public const int SupportedFormatVersion = 1;
    public const int SupportedSchemaVersion = 1;
    public const string RepositoryName = "KiYouJyo/UrbanPlanToolbox_Data";
    public const string CatalogUrl = DataPackCatalogService.CatalogUrl;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
    private static readonly HttpClient SharedHttpClient = CreateHttpClient();

    private readonly DataPackCatalogService _catalog;
    private readonly DataPackInstaller _installer;
    private readonly DataPackResolver _resolver;
    private readonly SemaphoreSlim _bundledPackGate = new(1, 1);

    public static ReferenceDataPackService Default { get; } = CreateDefault();

    public ReferenceDataPackService(IAppDataPathProvider paths, HttpClient? httpClient = null)
    {
        var http = httpClient ?? SharedHttpClient;
        var stateStore = new DataPackStateStore(paths ?? throw new ArgumentNullException(nameof(paths)));
        _catalog = new DataPackCatalogService(http);
        _installer = new DataPackInstaller(stateStore, http);
        _resolver = new DataPackResolver(stateStore, _installer);
    }

    private static ReferenceDataPackService CreateDefault() => new(AppDataPathProvider.Default, SharedHttpClient);

    public async Task<ReferenceDataPackContent?> LoadActiveAsync(string packId, CancellationToken cancellationToken = default)
    {
        ValidatePackId(packId);
        await EnsureBundledPackCurrentAsync(packId, cancellationToken).ConfigureAwait(false);
        return await _resolver.ResolveActiveAsync(packId, cancellationToken).ConfigureAwait(false);
    }

    public Task<ReferenceDataPackState?> GetActiveStateAsync(string packId, CancellationToken cancellationToken = default) => _resolver.GetActiveStateAsync(packId, cancellationToken);
    public Task<IReadOnlyList<ReferenceDataPackState>> GetInstalledVersionsAsync(string packId, CancellationToken cancellationToken = default) => _resolver.GetInstalledVersionsAsync(packId, cancellationToken);
    public Task<ReferenceDataPackState> ImportAsync(string packId, string sourcePath, string sourceKind = "local", CancellationToken cancellationToken = default) => _installer.InstallFromFileAsync(packId, sourcePath, sourceKind, cancellationToken);
    public Task<bool> RollbackAsync(string packId, CancellationToken cancellationToken = default) => _resolver.RollbackAsync(packId, cancellationToken);

    public async Task<ReferenceDataPackUpdateInfo> CheckForUpdateAsync(string packId, CancellationToken cancellationToken = default)
    {
        ValidatePackId(packId);
        var local = await _resolver.GetActiveStateAsync(packId, cancellationToken).ConfigureAwait(false);
        return await _catalog.CheckForUpdateAsync(packId, local, cancellationToken).ConfigureAwait(false);
    }

    public Task<ReferenceDataPackState> DownloadAndInstallAsync(string packId, ReferenceDataPackCatalogEntry entry, CancellationToken cancellationToken = default) => _installer.DownloadAndInstallAsync(packId, entry, cancellationToken);

    private async Task EnsureBundledPackCurrentAsync(string packId, CancellationToken cancellationToken)
    {
        var bundledDirectory = Path.Combine(AppContext.BaseDirectory, "Assets", "DataPacks", "Bundled");
        if (!Directory.Exists(bundledDirectory)) return;

        await _bundledPackGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var current = await _resolver.GetActiveStateAsync(packId, cancellationToken).ConfigureAwait(false);
            var currentVersion = ParseDataVersion(current?.Version);
            string? bestPath = null;
            var bestVersion = DataPackVersion.Zero;

            foreach (var archivePath in Directory.EnumerateFiles(bundledDirectory, $"{packId}-*.uptdata", SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var validated = await _installer.ValidateArchiveAsync(packId, archivePath, cancellationToken).ConfigureAwait(false);
                    var candidateVersion = ParseDataVersion(validated.Manifest.Version);
                    if (candidateVersion.CompareTo(bestVersion) <= 0) continue;
                    bestVersion = candidateVersion;
                    bestPath = archivePath;
                }
                catch (Exception exception) when (exception is InvalidDataException or IOException or JsonException)
                {
                    AppLogger.Default.Warning(nameof(ReferenceDataPackService), "bundled_pack_skipped", $"{Path.GetFileName(archivePath)}: {exception.Message}");
                }
            }

            if (bestPath is null || bestVersion == DataPackVersion.Zero || bestVersion.CompareTo(currentVersion) <= 0) return;

            var installed = await _installer.InstallFromFileAsync(packId, bestPath, "bundled", cancellationToken).ConfigureAwait(false);
            AppLogger.Default.Info(nameof(ReferenceDataPackService), "bundled_pack_activated", $"{packId}@{installed.Version}");
        }
        finally
        {
            _bundledPackGate.Release();
        }
    }

    public static string GetLocalized(IReadOnlyDictionary<string, string>? values, string language)
    {
        if (values is null || values.Count == 0) return string.Empty;
        if (values.TryGetValue(language, out var exact) && !string.IsNullOrWhiteSpace(exact)) return exact;
        var neutral = language.StartsWith("ja", StringComparison.OrdinalIgnoreCase) ? "ja-JP" : language.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "en-US" : "zh-CN";
        if (values.TryGetValue(neutral, out var value) && !string.IsNullOrWhiteSpace(value)) return value;
        return values.Values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }

    public static PlanningRegulationsPackDocument ParseRegulations(string json)
    {
        var document = JsonSerializer.Deserialize<PlanningRegulationsPackDocument>(json, JsonOptions) ?? throw new InvalidDataException("Regulations data is empty.");
        if (document.SchemaVersion != SupportedSchemaVersion || document.Entries.Count == 0 || document.Entries.Any(entry => string.IsNullOrWhiteSpace(entry.StableId) || string.IsNullOrWhiteSpace(entry.OriginalTitle) || string.IsNullOrWhiteSpace(entry.OfficialUrl)))
            throw new InvalidDataException("Regulations data does not match schema 1.");
        if (document.Entries.Select(entry => entry.StableId).Distinct(StringComparer.Ordinal).Count() != document.Entries.Count) throw new InvalidDataException("Regulations stable IDs are not unique.");
        return document;
    }

    public static PlanningTerminologyPackDocument ParseTerminology(string json)
    {
        var document = JsonSerializer.Deserialize<PlanningTerminologyPackDocument>(json, JsonOptions) ?? throw new InvalidDataException("Terminology data is empty.");
        if (document.SchemaVersion != SupportedSchemaVersion || document.Terms.Count == 0 || document.Terms.Any(term => string.IsNullOrWhiteSpace(term.StableId) || string.IsNullOrWhiteSpace(term.ZhCN) || string.IsNullOrWhiteSpace(term.JaJP) || string.IsNullOrWhiteSpace(term.EnUS)))
            throw new InvalidDataException("Terminology data does not match schema 1.");
        if (document.Terms.Select(term => term.StableId).Distinct(StringComparer.Ordinal).Count() != document.Terms.Count) throw new InvalidDataException("Terminology stable IDs are not unique.");
        return document;
    }

    public static DesignConceptsPackDocument ParseDesignConcepts(string json)
    {
        var document = JsonSerializer.Deserialize<DesignConceptsPackDocument>(json, JsonOptions) ?? throw new InvalidDataException("Design concepts data is empty.");
        if (document.SchemaVersion != SupportedSchemaVersion || document.Entries.Count == 0 || document.Entries.Any(entry => string.IsNullOrWhiteSpace(entry.StableId) || !new[] { "zh-CN", "ja-JP", "en-US" }.All(entry.Title.ContainsKey) || !new[] { "zh-CN", "ja-JP", "en-US" }.All(entry.Definition.ContainsKey) || entry.SourceIds.Count == 0))
            throw new InvalidDataException("Design concepts data does not match schema 1.");
        if (document.Entries.Select(entry => entry.StableId).Distinct(StringComparer.Ordinal).Count() != document.Entries.Count) throw new InvalidDataException("Design-concept stable IDs are not unique.");
        return document;
    }

    internal static void ValidateFeatureData(string packId, string dataJson)
    {
        switch (packId)
        {
            case ReferenceDataPackIds.PlanningRegulations:
                ParseRegulations(dataJson);
                break;
            case ReferenceDataPackIds.PlanningTerminology:
                ParseTerminology(dataJson);
                break;
            case ReferenceDataPackIds.DesignConcepts:
                ParseDesignConcepts(dataJson);
                break;
            default:
                throw new InvalidDataException("Unsupported reference data pack ID.");
        }
    }

    internal static void ValidatePackId(string packId)
    {
        if (packId is not (ReferenceDataPackIds.PlanningRegulations or ReferenceDataPackIds.PlanningTerminology or ReferenceDataPackIds.DesignConcepts)) throw new ArgumentOutOfRangeException(nameof(packId));
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"UrbanPlanToolbox/{AppVersionProvider.Version} DataPackClient");
        return client;
    }

    internal readonly record struct DataPackVersion(int Year, int Month, int Revision) : IComparable<DataPackVersion>
    {
        public static DataPackVersion Zero => new(0, 0, 0);
        public int CompareTo(DataPackVersion other)
        {
            var year = Year.CompareTo(other.Year); if (year != 0) return year;
            var month = Month.CompareTo(other.Month); if (month != 0) return month;
            return Revision.CompareTo(other.Revision);
        }
    }

    internal static DataPackVersion ParseDataVersion(string? value)
    {
        var parts = (value ?? string.Empty).Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 3 && int.TryParse(parts[0], out var year) && int.TryParse(parts[1], out var month) && int.TryParse(parts[2], out var revision) && year >= 2000 && month is >= 1 and <= 12 && revision >= 0
            ? new DataPackVersion(year, month, revision)
            : DataPackVersion.Zero;
    }
}
