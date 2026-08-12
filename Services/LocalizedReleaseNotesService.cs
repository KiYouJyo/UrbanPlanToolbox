using System.Net.Http.Json;
using System.Text.Json;
using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.Services;

/// <summary>Fetches display-only, version-matched notes. It never participates in installation.</summary>
public sealed class LocalizedReleaseNotesService : IReleaseNotesProvider
{
    public static LocalizedReleaseNotesService Default { get; } = new();
    private static readonly Uri BaseUri = new("https://kiyoujyo.github.io/UrbanPlanToolbox/release-notes/");
    private readonly HttpClient _client;

    public LocalizedReleaseNotesService(HttpClient? client = null) => _client = client ?? new HttpClient { Timeout = TimeSpan.FromSeconds(8) };

    public async Task<LocalizedReleaseNotes?> GetAsync(string version, string locale, CancellationToken cancellationToken = default)
    {
        if (!VersionParser.TryParseTag(version, out var expected)) return null;
        var normalizedLocale = NormalizeLocale(locale);
        try
        {
            var requestUri = new Uri(BaseUri, $"{expected.Major}.{expected.Minor}.{expected.Build}.json");
            var document = await _client.GetFromJsonAsync<LocalizedReleaseNotes>(requestUri, cancellationToken);
            if (document is null || document.SchemaVersion != 1 || !VersionMatches(document.Version, expected) ||
                !document.Notes.TryGetValue(normalizedLocale, out var note) || string.IsNullOrWhiteSpace(note.Title) || note.Items.Count == 0 || note.Items.Any(string.IsNullOrWhiteSpace)) return null;
            return document;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested) { return null; }
    }

    public static string NormalizeLocale(string locale) => locale switch
    {
        "zh-CN" or "ja-JP" or "en-US" => locale,
        _ => "en-US"
    };

    private static bool VersionMatches(string value, Version expected) => VersionParser.TryParseTag(value, out var actual) && actual == expected;
}
