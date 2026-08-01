namespace UrbanPlanToolbox.Services;

/// <summary>
/// Centralized localization access for C# dynamic text. Pages and services
/// must not create their own ResourceLoader instances.
/// </summary>
public interface ILocalizationService
{
    /// <summary>Returns the localized string for a stable resource key.</summary>
    string GetString(string resourceKey);

    /// <summary>Returns a localized format string populated with the given arguments.</summary>
    string GetFormattedString(string resourceKey, params object[] arguments);
}
