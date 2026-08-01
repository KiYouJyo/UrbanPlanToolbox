namespace UrbanPlanToolbox.Services;

/// <summary>
/// Maps the persisted language setting to BCP-47 overrides and back.
/// The internal stored value "system" means "follow the OS language".
/// </summary>
public static class LanguagePreference
{
    public const string SystemValue = "system";

    public static IReadOnlyList<string> SupportedBcp47Languages { get; } = ["zh-CN", "ja-JP", "en-US"];

    public static bool IsSupportedLanguage(string? language) =>
        SupportedBcp47Languages.Contains(language ?? string.Empty, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Returns the canonical stored value: "system" or the canonical BCP-47 tag.
    /// Invalid, empty, or corrupted values safely fall back to "system".
    /// </summary>
    public static string Normalize(string? storedValue)
    {
        if (string.IsNullOrWhiteSpace(storedValue))
        {
            return SystemValue;
        }

        var trimmed = storedValue.Trim();
        if (string.Equals(trimmed, SystemValue, StringComparison.OrdinalIgnoreCase))
        {
            return SystemValue;
        }

        return SupportedBcp47Languages.FirstOrDefault(
                   supported => string.Equals(supported, trimmed, StringComparison.OrdinalIgnoreCase))
               ?? SystemValue;
    }

    /// <summary>
    /// Returns the BCP-47 override to apply at startup, or null when the app
    /// should follow the system language.
    /// </summary>
    public static string? ResolveOverride(string? storedValue)
    {
        var normalized = Normalize(storedValue);
        return string.Equals(normalized, SystemValue, StringComparison.Ordinal) ? null : normalized;
    }

    /// <summary>
    /// Maps the first OS user language to a supported BCP-47 tag, falling back
    /// to zh-CN when the system language is unsupported or unknown.
    /// </summary>
    public static string ResolveSystemLanguage(IReadOnlyList<string>? systemLanguages)
    {
        var tag = systemLanguages?.FirstOrDefault(language => !string.IsNullOrWhiteSpace(language))?.Trim();
        if (string.IsNullOrEmpty(tag))
        {
            return "zh-CN";
        }

        var exact = SupportedBcp47Languages.FirstOrDefault(language =>
            string.Equals(language, tag, StringComparison.OrdinalIgnoreCase) ||
            tag.StartsWith(language + "-", StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact;
        }

        if (tag.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) return "zh-CN";
        if (tag.StartsWith("ja", StringComparison.OrdinalIgnoreCase)) return "ja-JP";
        if (tag.StartsWith("en", StringComparison.OrdinalIgnoreCase)) return "en-US";
        return "zh-CN";
    }

    /// <summary>
    /// Returns the effective BCP-47 language to apply at startup: the stored
    /// override when one exists, otherwise the system language mapped to a
    /// supported tag (zh-CN fallback).
    /// </summary>
    public static string ResolveEffectiveLanguage(string? storedValue, IReadOnlyList<string>? systemLanguages) =>
        ResolveOverride(storedValue) ?? ResolveSystemLanguage(systemLanguages);
}
