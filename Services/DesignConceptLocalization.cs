using System.Text.Json;

namespace UrbanPlanToolbox.Services;

/// <summary>
/// Resolves localized labels carried by the design-concepts data pack without
/// changing the canonical Chinese keys used for filtering and compatibility.
/// </summary>
public sealed class DesignConceptLocalization
{
    private readonly Dictionary<string, Dictionary<string, string>> _categories;
    private readonly Dictionary<string, Dictionary<string, string>> _projectTypes;
    private readonly Dictionary<string, Dictionary<string, string>> _tags;

    private DesignConceptLocalization(
        Dictionary<string, Dictionary<string, string>> categories,
        Dictionary<string, Dictionary<string, string>> projectTypes,
        Dictionary<string, Dictionary<string, string>> tags)
    {
        _categories = categories;
        _projectTypes = projectTypes;
        _tags = tags;
    }

    public static DesignConceptLocalization Empty { get; } = new(
        new(StringComparer.Ordinal),
        new(StringComparer.Ordinal),
        new(StringComparer.Ordinal));

    public static DesignConceptLocalization Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Empty;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("labels", out var labels) || labels.ValueKind != JsonValueKind.Object) return Empty;
            return new DesignConceptLocalization(
                ReadLabelGroup(labels, "categories"),
                ReadLabelGroup(labels, "projectTypes"),
                ReadLabelGroup(labels, "tags"));
        }
        catch (JsonException)
        {
            return Empty;
        }
    }

    public string Category(string key, string language) => Resolve(_categories, key, language);
    public string ProjectType(string key, string language) => Resolve(_projectTypes, key, language);
    public string Tag(string key, string language) => Resolve(_tags, key, language);

    public IReadOnlyList<string> ProjectTypes(IEnumerable<string> values, string language) =>
        values.Select(value => ProjectType(value, language)).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();

    public IReadOnlyList<string> Tags(IEnumerable<string> values, string language) =>
        values.Select(value => Tag(value, language)).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();

    public IEnumerable<string> SearchTerms(string category, IEnumerable<string> projectTypes, IEnumerable<string> tags)
    {
        foreach (var value in AllValues(_categories, category)) yield return value;
        foreach (var key in projectTypes)
            foreach (var value in AllValues(_projectTypes, key))
                yield return value;
        foreach (var key in tags)
            foreach (var value in AllValues(_tags, key))
                yield return value;
    }

    private static Dictionary<string, Dictionary<string, string>> ReadLabelGroup(JsonElement labels, string propertyName)
    {
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        if (!labels.TryGetProperty(propertyName, out var group) || group.ValueKind != JsonValueKind.Object) return result;
        foreach (var item in group.EnumerateObject())
        {
            if (item.Value.ValueKind != JsonValueKind.Object) continue;
            var translations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var translation in item.Value.EnumerateObject())
            {
                if (translation.Value.ValueKind != JsonValueKind.String) continue;
                var text = translation.Value.GetString();
                if (!string.IsNullOrWhiteSpace(text)) translations[translation.Name] = text.Trim();
            }
            if (translations.Count > 0) result[item.Name] = translations;
        }
        return result;
    }

    private static string Resolve(
        IReadOnlyDictionary<string, Dictionary<string, string>> group,
        string key,
        string language)
    {
        if (string.IsNullOrWhiteSpace(key)) return string.Empty;
        if (!group.TryGetValue(key, out var values) || values.Count == 0)
            return IsChinese(language) ? key : string.Empty;

        var locale = NormalizeLanguage(language);
        if (values.TryGetValue(locale, out var exact) && !string.IsNullOrWhiteSpace(exact)) return exact;
        return string.Empty;
    }

    private static IEnumerable<string> AllValues(
        IReadOnlyDictionary<string, Dictionary<string, string>> group,
        string key)
    {
        if (string.IsNullOrWhiteSpace(key)) yield break;
        yield return key;
        if (!group.TryGetValue(key, out var values)) yield break;
        foreach (var value in values.Values.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase))
            yield return value;
    }

    private static string NormalizeLanguage(string? language) =>
        language?.StartsWith("ja", StringComparison.OrdinalIgnoreCase) == true ? "ja-JP" :
        language?.StartsWith("en", StringComparison.OrdinalIgnoreCase) == true ? "en-US" : "zh-CN";

    private static bool IsChinese(string? language) => !(
        language?.StartsWith("ja", StringComparison.OrdinalIgnoreCase) == true ||
        language?.StartsWith("en", StringComparison.OrdinalIgnoreCase) == true);
}
