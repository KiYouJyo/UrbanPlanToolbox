using System.Globalization;
using Microsoft.Windows.ApplicationModel.Resources;
using Microsoft.Windows.Globalization;
using Windows.System.UserProfile;
using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.Services;

/// <summary>
/// MRT Core backed <see cref="ILocalizationService"/>. The default language is
/// zh-CN; missing keys resolve to an explicit placeholder instead of crashing.
/// </summary>
public sealed class LocalizationService : ILocalizationService
{
    private readonly object _gate = new();
    private readonly SettingsService _settingsService;
    private ResourceLoader _resourceLoader;
    private string _currentLanguage;
    private int _switchInProgress;

    public static LocalizationService Default { get; } = new(new SettingsService());

    public LocalizationService()
        : this(new SettingsService())
    {
    }

    public LocalizationService(SettingsService settingsService)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _resourceLoader = new ResourceLoader();
        _currentLanguage = LanguagePreference.ResolveEffectiveLanguage(
            ApplicationLanguages.PrimaryLanguageOverride,
            GlobalizationPreferences.Languages);
    }

    public string CurrentLanguage => _currentLanguage;

    public IReadOnlyList<LanguageOption> SupportedLanguages { get; } =
    [
        new("system", "Setting_Language_System.Content"),
        new("zh-CN", "Setting_Language_ZhCn.Content"),
        new("ja-JP", "Setting_Language_JaJp.Content"),
        new("en-US", "Setting_Language_EnUs.Content")
    ];

    public event EventHandler<LanguageChangedEventArgs>? LanguageChanged;

    /// <summary>Applies the persisted preference before the first Shell is created.</summary>
    public void ApplyPersistedLanguage(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var language = LanguagePreference.ResolveEffectiveLanguage(
            settings.Language,
            GlobalizationPreferences.Languages);
        ApplicationLanguages.PrimaryLanguageOverride = language;
        var culture = CultureInfo.GetCultureInfo(language);
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        lock (_gate)
        {
            _resourceLoader = new ResourceLoader();
            _currentLanguage = language;
        }
    }

    public string GetString(string resourceKey)
    {
        if (string.IsNullOrWhiteSpace(resourceKey))
        {
            return string.Empty;
        }

        try
        {
            ResourceLoader resourceLoader;
            lock (_gate) resourceLoader = _resourceLoader;
            var value = resourceLoader.GetString(MrtResourceKeyNormalizer.Normalize(resourceKey));
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

    public async Task<bool> SwitchLanguageAsync(string language, CancellationToken cancellationToken = default)
    {
        var requested = LanguagePreference.Normalize(language);
        if (string.Equals(requested, _currentLanguage, StringComparison.OrdinalIgnoreCase)) return true;
        if (Interlocked.Exchange(ref _switchInProgress, 1) != 0) return false;

        var previousLanguage = _currentLanguage;
        var previousOverride = ApplicationLanguages.PrimaryLanguageOverride;
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var effective = LanguagePreference.ResolveEffectiveLanguage(
                requested,
                GlobalizationPreferences.Languages);
            ApplicationLanguages.PrimaryLanguageOverride = effective;
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(effective);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(effective);
            var replacementLoader = new ResourceLoader();
            _settingsService.Update(settings => settings.Language = requested);
            lock (_gate)
            {
                _resourceLoader = replacementLoader;
                _currentLanguage = effective;
            }
            LanguageChanged?.Invoke(this, new LanguageChangedEventArgs(previousLanguage, effective));
            return true;
        }
        catch
        {
            ApplicationLanguages.PrimaryLanguageOverride = previousOverride;
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
            _currentLanguage = previousLanguage;
            return false;
        }
        finally
        {
            Volatile.Write(ref _switchInProgress, 0);
            await Task.CompletedTask;
        }
    }

    /// <summary>Explicit placeholder returned for unknown or unavailable keys.</summary>
    public static string CreatePlaceholder(string resourceKey) => $"!{resourceKey}!";
}
