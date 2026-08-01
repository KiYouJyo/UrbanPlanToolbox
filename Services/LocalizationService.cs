using System.Globalization;
using Microsoft.Windows.ApplicationModel.Resources;

namespace UrbanPlanToolbox.Services;

/// <summary>
/// MRT Core backed <see cref="ILocalizationService"/>. The default language is
/// zh-CN; missing keys resolve to an explicit placeholder instead of crashing.
/// </summary>
public sealed class LocalizationService : ILocalizationService
{
    private readonly ResourceLoader _resourceLoader;

    public static LocalizationService Default { get; } = new();

    public LocalizationService()
        : this(new ResourceLoader())
    {
    }

    public LocalizationService(ResourceLoader resourceLoader)
    {
        _resourceLoader = resourceLoader ?? throw new ArgumentNullException(nameof(resourceLoader));
    }

    public string GetString(string resourceKey)
    {
        if (string.IsNullOrWhiteSpace(resourceKey))
        {
            return string.Empty;
        }

        try
        {
            var value = _resourceLoader.GetString(resourceKey);
            return string.IsNullOrEmpty(value) ? CreatePlaceholder(resourceKey) : value;
        }
        catch (Exception)
        {
            // Unknown keys and MRT failures must never crash the UI.
            return CreatePlaceholder(resourceKey);
        }
    }

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

    /// <summary>Explicit placeholder returned for unknown or unavailable keys.</summary>
    public static string CreatePlaceholder(string resourceKey) => $"!{resourceKey}!";
}
