using System.Globalization;
using UrbanPlanToolbox.Services;

namespace UrbanPlanToolbox.Tests;

/// <summary>
/// Dictionary-backed localization used by tests. Missing keys return an
/// explicit placeholder, mirroring the production fallback behavior.
/// </summary>
internal sealed class DictionaryLocalizationService : ILocalizationService
{
    private readonly IReadOnlyDictionary<string, string> _values;

    public DictionaryLocalizationService(IReadOnlyDictionary<string, string> values)
    {
        _values = values;
    }

    public string CurrentLanguage => "en-US";
    public IReadOnlyList<LanguageOption> SupportedLanguages => [];
    public event EventHandler<LanguageChangedEventArgs>? LanguageChanged { add { } remove { } }
    public Task<bool> SwitchLanguageAsync(string language, CancellationToken cancellationToken = default) => Task.FromResult(true);

    public string GetString(string resourceKey) =>
        _values.TryGetValue(resourceKey, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : $"!{resourceKey}!";

    public string GetFormattedString(string resourceKey, params object[] arguments)
    {
        var template = GetString(resourceKey);
        try
        {
            return string.Format(CultureInfo.CurrentCulture, template, arguments);
        }
        catch (FormatException)
        {
            return template;
        }
    }
}

internal static class TestLocalization
{
    public static ILocalizationService ZhCn { get; } = For("zh-CN");
    public static ILocalizationService JaJp { get; } = For("ja-JP");
    public static ILocalizationService EnUs { get; } = For("en-US");

    public static ILocalizationService For(string language) =>
        new DictionaryLocalizationService(ReswCatalog.Load(language));
}
