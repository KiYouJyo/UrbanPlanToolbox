namespace UrbanPlanToolbox.Services;

public enum StartupSplashTheme
{
    Light,
    Dark
}

/// <summary>Resolves the startup artwork before the main visual tree is displayed.</summary>
public static class StartupSplashPresentation
{
    public const string DarkLogoAssetUri = "ms-appx:///Assets/Icon-Large-Dark-1024.png";
    public const string LightLogoAssetUri = "ms-appx:///Assets/Icon-Large-Light-1024.png";

    public static StartupSplashTheme ResolveTheme(string? preference, bool systemUsesLightTheme) =>
        SettingsService.NormalizeTheme(preference) switch
        {
            AppTheme.Light => StartupSplashTheme.Light,
            AppTheme.Dark => StartupSplashTheme.Dark,
            _ => systemUsesLightTheme ? StartupSplashTheme.Light : StartupSplashTheme.Dark
        };

    public static string GetLogoAssetUri(StartupSplashTheme theme) =>
        theme == StartupSplashTheme.Light ? LightLogoAssetUri : DarkLogoAssetUri;

    // The light artwork has a square canvas. This size preserves the visible
    // bounds of the existing 200%-scale native splash artwork at 100% display scale.
    public static double GetLogoCanvasSize(StartupSplashTheme theme) =>
        theme == StartupSplashTheme.Light ? 183 : double.NaN;
}
