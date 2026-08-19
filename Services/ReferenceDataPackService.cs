using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Models.Tools;

namespace UrbanPlanToolbox.Services;

public sealed class ReferenceDataPackService
{
    public const int SupportedFormatVersion = 1;
    public const int SupportedSchemaVersion = 1;
    public const string RepositoryName = "KiYouJyo/UrbanPlanToolbox_Data";
    public const string CatalogUrl = "https://raw.githubusercontent.com/KiYouJyo/UrbanPlanToolbox_Data/main/catalog/catalog.json";

    private const long MaxArchiveBytes = 64L * 1024 * 1024;
    private const long MaxExpandedBytes = 128L * 1024 * 1024;
    private const int MaxEntries = 64;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private static readonly HttpClient SharedHttpClient = CreateHttpClient();
    private readonly IAppDataPathProvider _paths;
    private readonly HttpClient _http;

    public static ReferenceDataPackService Default { get; } = new(AppDataPathProvider.Default, SharedHttpClient);

    public ReferenceDataPackService(IAppDataPathProvider paths, HttpClient? httpClient = null)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _http = httpClient ?? SharedHttpClient;
    }

    public async Task<ReferenceDataPackContent?> LoadActiveAsync(string packId, CancellationToken cancellationToken = default)
    {
        ValidatePackId(packId);
        var state = await ReadStateAsync(packId, cancellationToken).ConfigureAwait(false);
        if (state is null) return null;

        var directory = GetPackDirectory(packId);
        var archivePath = Path.Combine(directory, state.ArchiveFileName);
        if (!File.Exists(archivePath)) return null;
        var validated = await ValidateArchiveAsync(packId, archivePath, cancellationToken).ConfigureAwait(false);
        return new ReferenceDataPackContent(validated.Manifest, state, validated.DataJson, archivePath);
    }

    public async Task<ReferenceDataPackState?> GetActiveStateAsync(string packId, CancellationToken cancellationToken = default)
    {
        ValidatePackId(packId);
        return await ReadStateAsync(packId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ReferenceDataPackState>> GetInstalledVersionsAsync(string packId, CancellationToken cancellationToken = default)
    {
        ValidatePackId(packId);
        var directory = GetPackDirectory(packId);
        var states = new List<ReferenceDataPackState>();
        foreach (var archivePath in Directory.EnumerateFiles(directory, "*.uptdata", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var validated = await ValidateArchiveAsync(packId, archivePath, cancellationToken).ConfigureAwait(false);
                states.Add(new ReferenceDataPackState
                {
                    PackId = packId,
                    Version = validated.Manifest.Version,
                    SchemaVersion = validated.Manifest.SchemaVersion,
                    ArchiveFileName = Path.GetFileName(archivePath),
                    SourceKind = "installed",
                    InstalledAt = File.GetLastWriteTimeUtc(archivePath)
                });
            }
            catch (Exception exception) when (exception is InvalidDataException or IOException or JsonException)
            {
                AppLogger.Default.Warning(nameof(ReferenceDataPackService), "installed_pack_skipped", $"{Path.GetFileName(archivePath)}: {exception.Message}");
            }
        }

        return states.OrderByDescending(state => ParseDataVersion(state.Version)).ToArray();
    }

    public async Task<ReferenceDataPackState> ImportAsync(string packId, string sourcePath, string sourceKind = "local", CancellationToken cancellationToken = default)
    {
        ValidatePackId(packId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        var source = Path.GetFullPath(sourcePath);
        if (!File.Exists(source)) throw new FileNotFoundException("The selected .uptdata file does not exist.", source);
        var length = new FileInfo(source).Length;
        if (length <= 0 || length > MaxArchiveBytes) throw new InvalidDataException("The .uptdata archive is empty or exceeds the supported size limit.");

        var validated = await ValidateArchiveAsync(packId, source, cancellationToken).ConfigureAwait(false);
        var directory = GetPackDirectory(packId);
        var fileName = SanitizeArchiveFileName($"{packId}-{validated.Manifest.Version}.uptdata");
        var destination = Path.Combine(directory, fileName);
        var temp = destination + ".tmp";
        File.Copy(source, temp, true);
        File.Move(temp, destination, true);

        var state = new ReferenceDataPackState
        {
            PackId = packId,
            Version = validated.Manifest.Version,
            SchemaVersion = validated.Manifest.SchemaVersion,
            ArchiveFileName = fileName,
            SourceKind = string.IsNullOrWhiteSpace(sourceKind) ? "local" : sourceKind,
            InstalledAt = DateTimeOffset.UtcNow
        };
        await WriteStateAsync(packId, state, cancellationToken).ConfigureAwait(false);
        AppLogger.Default.Info(nameof(ReferenceDataPackService), "pack_activated", $"{packId}@{state.Version} ({state.SourceKind})");
        return state;
    }

    public async Task<bool> RollbackAsync(string packId, CancellationToken cancellationToken = default)
    {
        var current = await ReadStateAsync(packId, cancellationToken).ConfigureAwait(false);
        if (current is null) return false;
        var installed = await GetInstalledVersionsAsync(packId, cancellationToken).ConfigureAwait(false);
        var currentVersion = ParseDataVersion(current.Version);
        var previous = installed
            .Where(state => ParseDataVersion(state.Version).CompareTo(currentVersion) < 0)
            .OrderByDescending(state => ParseDataVersion(state.Version))
            .FirstOrDefault();
        if (previous is null) return false;
        await WriteStateAsync(packId, previous with { SourceKind = "rollback", InstalledAt = DateTimeOffset.UtcNow }, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<ReferenceDataPackUpdateInfo> CheckForUpdateAsync(string packId, CancellationToken cancellationToken = default)
    {
        ValidatePackId(packId);
        var local = await ReadStateAsync(packId, cancellationToken).ConfigureAwait(false);
        ReferenceDataPackCatalogEntry? remote;
        try
        {
            using var response = await _http.GetAsync(CatalogUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return new(null, local, false, $"catalog-http-{(int)response.StatusCode}");
            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            remote = FindCatalogEntry(json, packId);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            AppLogger.Default.Warning(nameof(ReferenceDataPackService), "catalog_check_failed", exception.Message);
            return new(null, local, false, "catalog-unavailable");
        }

        if (remote is null) return new(null, local, false, "not-published");
        var updateAvailable = local is null || ParseDataVersion(remote.Version).CompareTo(ParseDataVersion(local.Version)) > 0;
        return new(remote, local, updateAvailable, updateAvailable ? "update-available" : "latest");
    }

    public async Task<ReferenceDataPackState> DownloadAndInstallAsync(string packId, ReferenceDataPackCatalogEntry entry, CancellationToken cancellationToken = default)
    {
        ValidatePackId(packId);
        if (!string.Equals(entry.PackId, packId, StringComparison.Ordinal)) throw new InvalidDataException("Catalog pack ID does not match the requested data pack.");
        if (!Uri.TryCreate(entry.DownloadUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps) throw new InvalidDataException("The catalog download URL is invalid.");

        var directory = GetPackDirectory(packId);
        var downloadPath = Path.Combine(directory, $".{packId}-{Guid.NewGuid():N}.download");
        try
        {
            using var response = await _http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var contentLength = response.Content.Headers.ContentLength;
            if (contentLength is > MaxArchiveBytes) throw new InvalidDataException("The remote data pack exceeds the supported size limit.");
            await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            await using (var output = new FileStream(downloadPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
            {
                await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            }
            if (new FileInfo(downloadPath).Length > MaxArchiveBytes) throw new InvalidDataException("The downloaded data pack exceeds the supported size limit.");
            if (!string.IsNullOrWhiteSpace(entry.Sha256)) VerifySha256(downloadPath, entry.Sha256);
            return await ImportAsync(packId, downloadPath, "official", cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            try { if (File.Exists(downloadPath)) File.Delete(downloadPath); } catch { }
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
        if (document.SchemaVersion != SupportedSchemaVersion || document.Entries.Any(entry => string.IsNullOrWhiteSpace(entry.StableId) || string.IsNullOrWhiteSpace(entry.OriginalTitle))) throw new InvalidDataException("Regulations data does not match schema 1.");
        return document;
    }

    public static PlanningTerminologyPackDocument ParseTerminology(string json)
    {
        var document = JsonSerializer.Deserialize<PlanningTerminologyPackDocument>(json, JsonOptions) ?? throw new InvalidDataException("Terminology data is empty.");
        if (document.SchemaVersion != SupportedSchemaVersion || document.Terms.Any(term => string.IsNullOrWhiteSpace(term.StableId) || string.IsNullOrWhiteSpace(term.ZhCN) || string.IsNullOrWhiteSpace(term.JaJP) || string.IsNullOrWhiteSpace(term.EnUS))) throw new InvalidDataException("Terminology data does not match schema 1.");
        return document;
    }

    public static DesignConceptsPackDocument ParseDesignConcepts(string json)
    {
        var document = JsonSerializer.Deserialize<DesignConceptsPackDocument>(json, JsonOptions) ?? throw new InvalidDataException("Design concepts data is empty.");
        if (document.SchemaVersion != SupportedSchemaVersion || document.Entries.Any(entry => string.IsNullOrWhiteSpace(entry.StableId) || entry.Title.Count == 0 || entry.Definition.Count == 0)) throw new InvalidDataException("Design concepts data does not match schema 1.");
        return document;
    }

    internal static ReferenceDataPackCatalogEntry? FindCatalogEntry(string json, string packId)
    {
        using var document = JsonDocument.Parse(json);
        ReferenceDataPackCatalogEntry? best = null;
        Visit(document.RootElement);
        return best;

        void Visit(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                if (TryReadCatalogObject(element, packId, out var candidate) && (best is null || ParseDataVersion(candidate.Version).CompareTo(ParseDataVersion(best.Version)) > 0)) best = candidate;
                foreach (var property in element.EnumerateObject()) Visit(property.Value);
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray()) Visit(item);
            }
        }
    }

    private static bool TryReadCatalogObject(JsonElement element, string packId, out ReferenceDataPackCatalogEntry entry)
    {
        entry = null!;
        var id = ReadString(element, "id", "packId");
        var version = ReadString(element, "version", "dataVersion");
        if (!string.Equals(id, packId, StringComparison.Ordinal) || string.IsNullOrWhiteSpace(version)) return false;
        var downloadUrl = ReadString(element, "downloadUrl", "assetUrl", "url") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            var tag = ReadString(element, "releaseTag", "tag");
            var fileName = ReadString(element, "fileName", "assetName");
            if (!string.IsNullOrWhiteSpace(tag) && !string.IsNullOrWhiteSpace(fileName)) downloadUrl = $"https://github.com/{RepositoryName}/releases/download/{Uri.EscapeDataString(tag)}/{Uri.EscapeDataString(fileName)}";
        }
        var schema = ReadInt(element, "schemaVersion") ?? SupportedSchemaVersion;
        var minApp = ReadString(element, "minAppVersion") ?? string.Empty;
        var sha = ReadString(element, "sha256", "sha");
        var name = ReadString(element, "fileName", "assetName");
        var size = ReadLong(element, "sizeBytes", "size");
        entry = new ReferenceDataPackCatalogEntry(packId, version, schema, minApp, downloadUrl, sha, name, size);
        return true;
    }

    private async Task<(ReferenceDataPackManifest Manifest, string DataJson)> ValidateArchiveAsync(string packId, string archivePath, CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        using var archive = ZipFile.OpenRead(archivePath);
        if (archive.Entries.Count == 0 || archive.Entries.Count > MaxEntries) throw new InvalidDataException("The .uptdata archive has an invalid number of entries.");
        long expandedBytes = 0;
        foreach (var zipEntry in archive.Entries)
        {
            ValidateArchivePath(zipEntry.FullName);
            expandedBytes += zipEntry.Length;
            if (expandedBytes > MaxExpandedBytes) throw new InvalidDataException("The .uptdata archive expands beyond the supported size limit.");
        }

        var manifestEntry = archive.GetEntry("manifest.json") ?? archive.GetEntry("manifest.source.json") ?? throw new InvalidDataException("The .uptdata archive does not contain manifest.json.");
        ReferenceDataPackManifest manifest;
        await using (var stream = manifestEntry.Open())
        {
            manifest = await JsonSerializer.DeserializeAsync<ReferenceDataPackManifest>(stream, JsonOptions, cancellationToken).ConfigureAwait(false) ?? throw new InvalidDataException("The data pack manifest is empty.");
        }
        ValidateManifest(packId, manifest);
        var dataEntry = archive.GetEntry(manifest.DataPath) ?? throw new InvalidDataException($"The .uptdata archive does not contain {manifest.DataPath}.");
        string dataJson;
        using (var reader = new StreamReader(dataEntry.Open())) dataJson = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        ValidateFeatureData(packId, dataJson);
        return (manifest, dataJson);
    }

    private static void ValidateFeatureData(string packId, string dataJson)
    {
        _ = packId switch
        {
            ReferenceDataPackIds.PlanningRegulations => ParseRegulations(dataJson),
            ReferenceDataPackIds.PlanningTerminology => ParseTerminology(dataJson),
            ReferenceDataPackIds.DesignConcepts => ParseDesignConcepts(dataJson),
            _ => throw new InvalidDataException("Unsupported reference data pack ID.")
        };
    }

    private static void ValidateManifest(string packId, ReferenceDataPackManifest manifest)
    {
        if (manifest.FormatVersion != SupportedFormatVersion) throw new InvalidDataException("Unsupported .uptdata format version.");
        if (!string.Equals(manifest.Id, packId, StringComparison.Ordinal)) throw new InvalidDataException("The .uptdata pack ID does not match this library.");
        if (manifest.SchemaVersion != SupportedSchemaVersion) throw new InvalidDataException("Unsupported data schema version.");
        if (!string.Equals(manifest.Publisher, "UrbanPlanToolbox", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The data pack publisher is not trusted.");
        if (string.IsNullOrWhiteSpace(manifest.Version) || ParseDataVersion(manifest.Version) == DataPackVersion.Zero) throw new InvalidDataException("The data pack version is invalid.");
        var dataPath = manifest.DataPath.Replace('\\', '/');
        if (!dataPath.StartsWith("data/", StringComparison.Ordinal) || !dataPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The data path in the manifest is invalid.");
        ValidateArchivePath(dataPath);
        if (!string.IsNullOrWhiteSpace(manifest.MinAppVersion) && VersionParser.TryParseTag(manifest.MinAppVersion, out var minimum) && AppVersionProvider.GetCurrentVersion() < minimum)
            throw new InvalidDataException($"This data pack requires UrbanPlanToolbox {manifest.MinAppVersion} or later.");
    }

    private async Task<ReferenceDataPackState?> ReadStateAsync(string packId, CancellationToken cancellationToken)
    {
        var path = GetStatePath(packId);
        if (!File.Exists(path)) return null;
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
            var state = await JsonSerializer.DeserializeAsync<ReferenceDataPackState>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
            return state is { PackId.Length: > 0 } && string.Equals(state.PackId, packId, StringComparison.Ordinal) ? state : null;
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            AppLogger.Default.Warning(nameof(ReferenceDataPackService), "pack_state_read_failed", exception.Message);
            return null;
        }
    }

    private async Task WriteStateAsync(string packId, ReferenceDataPackState state, CancellationToken cancellationToken)
    {
        var path = GetStatePath(packId);
        var temp = path + ".tmp";
        await using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
            await JsonSerializer.SerializeAsync(stream, state, JsonOptions, cancellationToken).ConfigureAwait(false);
        File.Move(temp, path, true);
    }

    private string GetStatePath(string packId) => Path.Combine(GetPackDirectory(packId), "active-pack.json");

    private string GetPackDirectory(string packId)
    {
        var toolId = packId switch
        {
            ReferenceDataPackIds.PlanningRegulations => ToolIds.RegulationsIndex,
            ReferenceDataPackIds.PlanningTerminology => ToolIds.PlanningTerminology,
            ReferenceDataPackIds.DesignConcepts => ToolIds.DesignConceptDictionary,
            _ => throw new ArgumentOutOfRangeException(nameof(packId))
        };
        return _paths.GetToolDataDirectory(toolId);
    }

    private static void ValidatePackId(string packId)
    {
        if (packId is not (ReferenceDataPackIds.PlanningRegulations or ReferenceDataPackIds.PlanningTerminology or ReferenceDataPackIds.DesignConcepts)) throw new ArgumentOutOfRangeException(nameof(packId));
    }

    private static void ValidateArchivePath(string path)
    {
        var normalized = path.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(normalized) || normalized.StartsWith('/', StringComparison.Ordinal) || normalized.Contains(':', StringComparison.Ordinal)) throw new InvalidDataException("The .uptdata archive contains an unsafe path.");
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => segment is "." or "..")) throw new InvalidDataException("The .uptdata archive contains path traversal.");
    }

    private static string SanitizeArchiveFileName(string fileName)
    {
        var value = string.Concat(fileName.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
        return value.EndsWith(".uptdata", StringComparison.OrdinalIgnoreCase) ? value : value + ".uptdata";
    }

    private static void VerifySha256(string path, string expected)
    {
        var normalizedExpected = expected.Trim().Replace("sha256:", string.Empty, StringComparison.OrdinalIgnoreCase).Replace("-", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        using var stream = File.OpenRead(path);
        var actual = Convert.ToHexString(SHA256.HashData(stream));
        if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(actual), Convert.FromHexString(normalizedExpected))) throw new InvalidDataException("The downloaded data pack failed SHA-256 verification.");
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"UrbanPlanToolbox/{AppVersionProvider.Version} DataPackClient");
        return client;
    }

    private static string? ReadString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
            if (element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String) return property.GetString();
        return null;
    }

    private static int? ReadInt(JsonElement element, params string[] names)
    {
        foreach (var name in names)
            if (element.TryGetProperty(name, out var property) && property.TryGetInt32(out var value)) return value;
        return null;
    }

    private static long? ReadLong(JsonElement element, params string[] names)
    {
        foreach (var name in names)
            if (element.TryGetProperty(name, out var property) && property.TryGetInt64(out var value)) return value;
        return null;
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
