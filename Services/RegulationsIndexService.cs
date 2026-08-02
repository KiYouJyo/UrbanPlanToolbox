using System.Text.Json;
using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.Services;

public sealed class RegulationsIndexService
{
    public const int RegulationsIndexDataVersion = 1;
    public const string DataFileName = "regulations-index.v1.json";
    private readonly RegulationsIndexDocument _data;
    private static readonly JsonSerializerOptions IndexJsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public RegulationsIndexService(RegulationsIndexDocument data)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
        Validate(_data);
    }

    public static RegulationsIndexService LoadPackaged()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Data", "RegulationsIndex", DataFileName);
        return new(Deserialize(File.ReadAllText(path)));
    }

    public static RegulationsIndexDocument Deserialize(string json) =>
        JsonSerializer.Deserialize<RegulationsIndexDocument>(json, IndexJsonOptions)
        ?? throw new InvalidDataException("Regulations index JSON is empty.");

    public RegulationsIndexDocument Data => _data;

    public IReadOnlyList<RegulationEntry> Search(string? query, string? region = null, string? jurisdictionLevel = null, string? topic = null, string? documentLevel = null)
    {
        var q = query?.Trim() ?? string.Empty;
        return _data.Entries.Where(entry =>
                MatchesFilter(entry.Region, region) &&
                MatchesFilter(entry.JurisdictionLevel, jurisdictionLevel) &&
                MatchesFilter(entry.Topic, topic) &&
                MatchesFilter(entry.DocumentLevel, documentLevel) &&
                (q.Length == 0 || string.Join("\n", entry.OriginalTitle, entry.ChineseTitle, entry.IdentifierOrYear, entry.Topic, entry.DocumentLevel, entry.ScopeAndPurpose, entry.DownloadAndCopyrightNote, entry.SearchKeywords).Contains(q, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(entry => entry.Id)
            .ToArray();
    }

    private static bool MatchesFilter(string? value, string? filter) => string.IsNullOrWhiteSpace(filter) || string.Equals(value, filter, StringComparison.Ordinal);

    public static void Validate(RegulationsIndexDocument data)
    {
        if (data.DataVersion != RegulationsIndexDataVersion || data.Entries.Count != 221 || data.OfficialPortals.Count != 20)
            throw new InvalidDataException("Regulations index counts or version are invalid.");
        if (data.Entries.Select(entry => entry.Id).Distinct().Count() != data.Entries.Count)
            throw new InvalidDataException("Regulations index IDs must be unique.");
        if (data.Entries.Any(entry => string.IsNullOrWhiteSpace(entry.OriginalTitle) || string.IsNullOrWhiteSpace(entry.ScopeAndPurpose) || string.IsNullOrWhiteSpace(entry.VerifiedDate) || (string.IsNullOrWhiteSpace(entry.OfficialUrl) && string.IsNullOrWhiteSpace(entry.DownloadUrl))))
            throw new InvalidDataException("Regulations index entries are missing required fields.");
        if (data.Entries.Any(entry => entry.VerifiedDate != "2026-07-31"))
            throw new InvalidDataException("Regulations index verification date is invalid.");
        foreach (var link in data.Entries.SelectMany(entry => new[] { entry.OfficialUrl, entry.DownloadUrl }).Concat(data.OfficialPortals.Select(portal => portal.Url)).Where(link => link is not null))
            if (!Uri.TryCreate(link, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) throw new InvalidDataException("Regulations index contains a non-http URL.");
    }
}
