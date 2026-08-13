namespace UrbanPlanToolbox.Services;

public enum StartupSplashTheme
{
    Light,
    Dark
}

/// <summary>Resolves the startup artwork before the main visual tree is displayed.</summary>
public static class StartupSplashPresentation
{
    // Historical file names remain for package stability. Semantic names describe
    // the target app theme: Dark uses the white source and Light the black source.
    private const string WhiteLogoSourceUri = "ms-appx:///Assets/Icon-Large-Dark-1024.png";
    private const string BlackLogoSourceUri = "ms-appx:///Assets/Icon-Large-Light-1024.png";
    public const string LogoForDarkThemeAssetUri = WhiteLogoSourceUri;
    public const string LogoForLightThemeAssetUri = BlackLogoSourceUri;

    public static StartupSplashTheme ResolveTheme(string? preference, bool systemUsesLightTheme) =>
        SettingsService.NormalizeTheme(preference) switch
        {
            AppTheme.Light => StartupSplashTheme.Light,
            AppTheme.Dark => StartupSplashTheme.Dark,
            _ => systemUsesLightTheme ? StartupSplashTheme.Light : StartupSplashTheme.Dark
        };

    public static string GetLogoAssetUri(StartupSplashTheme theme) =>
        theme == StartupSplashTheme.Light ? LogoForLightThemeAssetUri : LogoForDarkThemeAssetUri;

    // The light artwork has a square canvas. This size preserves the visible
    // bounds of the existing 200%-scale native splash artwork at 100% display scale.
    public static double GetLogoCanvasSize(StartupSplashTheme theme) =>
        theme == StartupSplashTheme.Light ? 183 : double.NaN;
}
