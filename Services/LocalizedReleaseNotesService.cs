using System.Net.Http.Json;
using System.Text.Json;
using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.Services;

/// <summary>Fetches display-only, version-matched notes. It never participates in installation.</summary>
public sealed class LocalizedReleaseNotesService : IReleaseNotesProvider
{
    public static LocalizedReleaseNotesService Default { get; } = new();
    private static readonly Uri BaseUri = new("https://kiyoujyo.github.io/UrbanPlanToolbox/release-notes/");
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly HttpClient _client;
    private readonly Func<string, CancellationToken, Task<string?>> _bundledNotesLoader;

    public LocalizedReleaseNotesService(HttpClient? client = null, Func<string, CancellationToken, Task<string?>>? bundledNotesLoader = null)
    {
        _client = client ?? new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        _bundledNotesLoader = bundledNotesLoader ?? ReadBundledNotesAsync;
    }

    public async Task<LocalizedReleaseNotes?> GetAsync(string version, string locale, CancellationToken cancellationToken = default)
    {
        if (!VersionParser.TryParseTag(version, out var expected))
        {
            AppLogger.Default.Warning("ReleaseNotes", "ReleaseNotesLocalizationFailed", $"Version={version}; Locale={locale}; Reason=InvalidVersion");
            return null;
        }
        var normalizedLocale = NormalizeLocale(locale);
        var normalizedVersion = $"{expected.Major}.{expected.Minor}.{expected.Build}";
        AppLogger.Default.Info("ReleaseNotes", "ReleaseNotesLocalizationRequested", $"Version={normalizedVersion}; RequestedLocale={locale}; NormalizedLocale={normalizedLocale}");

        var bundled = await LoadBundledAsync(normalizedVersion, expected, normalizedLocale, cancellationToken);
        if (bundled is not null) return bundled;
        try
        {
            var requestUri = new Uri(BaseUri, $"{normalizedVersion}.json");
            var document = await _client.GetFromJsonAsync<LocalizedReleaseNotes>(requestUri, cancellationToken);
            if (IsUsable(document, expected, normalizedLocale))
            {
                LogLoaded(normalizedVersion, document!, normalizedLocale, "RemoteSameLocale");
                return document;
            }
            AppLogger.Default.Warning("ReleaseNotes", "ReleaseNotesLocalizationFailed", $"Version={normalizedVersion}; Locale={normalizedLocale}; Reason=NoMatchingLocaleInRemoteDocument");
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            AppLogger.Default.Warning("ReleaseNotes", "ReleaseNotesLocalizationFailed", $"Version={normalizedVersion}; Locale={normalizedLocale}; Reason=RemoteReadFailed; Type={exception.GetType().Name}");
        }
        return null;
    }

    public static string NormalizeLocale(string locale) => locale.Trim() switch
    {
        "zh-CN" or "ja-JP" or "en-US" => locale,
        var value when value.StartsWith("zh", StringComparison.OrdinalIgnoreCase) => "zh-CN",
        var value when value.StartsWith("ja", StringComparison.OrdinalIgnoreCase) => "ja-JP",
        _ => "en-US"
    };

    private static bool VersionMatches(string value, Version expected) => VersionParser.TryParseTag(value, out var actual) && actual == expected;

    private static bool IsUsable(LocalizedReleaseNotes? document, Version expected, string locale) =>
        document is not null && document.SchemaVersion == 1 && VersionMatches(document.Version, expected) &&
        document.Notes.TryGetValue(locale, out var note) && !string.IsNullOrWhiteSpace(note.Title) && note.Items.Count > 0 && !note.Items.Any(string.IsNullOrWhiteSpace);

    private async Task<LocalizedReleaseNotes?> LoadBundledAsync(string version, Version expected, string locale, CancellationToken cancellationToken)
    {
        try
        {
            var content = await _bundledNotesLoader(version, cancellationToken);
            if (string.IsNullOrWhiteSpace(content))
            {
                AppLogger.Default.Warning("ReleaseNotes", "ReleaseNotesLocalizationFailed", $"Version={version}; Locale={locale}; Reason=BundledFileNotFound");
                return null;
            }
            var document = JsonSerializer.Deserialize<LocalizedReleaseNotes>(content, JsonOptions);
            if (!IsUsable(document, expected, locale))
            {
                AppLogger.Default.Warning("ReleaseNotes", "ReleaseNotesLocalizationFailed", $"Version={version}; Locale={locale}; Reason=BundledDocumentMissingRequestedLocale");
                return null;
            }
            LogLoaded(version, document!, locale, "BundledPackage");
            return document;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            AppLogger.Default.Warning("ReleaseNotes", "ReleaseNotesLocalizationFailed", $"Version={version}; Locale={locale}; Reason=BundledReadFailed; Type={exception.GetType().Name}");
            return null;
        }
    }

    private static Task<string?> ReadBundledNotesAsync(string version, CancellationToken cancellationToken)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Data", "ReleaseNotes", $"{version}.json");
        if (!File.Exists(path)) return Task.FromResult<string?>(null);
        return ReadAsync(path, cancellationToken);
    }

    private static async Task<string?> ReadAsync(string path, CancellationToken cancellationToken) => await File.ReadAllTextAsync(path, cancellationToken);

    private static void LogLoaded(string version, LocalizedReleaseNotes document, string locale, string origin) =>
        AppLogger.Default.Info("ReleaseNotes", "ReleaseNotesLocalizationLoaded", $"Version={version}; AvailableLocales={string.Join(',', document.Notes.Keys.Order(StringComparer.OrdinalIgnoreCase))}; SelectedLocale={locale}; ItemCount={document.Notes[locale].Items.Count}; Origin={origin}");
}
