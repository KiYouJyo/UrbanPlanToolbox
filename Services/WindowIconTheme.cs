namespace UrbanPlanToolbox.Services;

/// <summary>
/// Maps the resolved application theme to window-chrome assets.
/// Theme names describe the target environment, not the foreground color:
/// Dark uses the white source; Light uses the black source. Shell assets are
/// selected independently by MRT theme qualifiers and are not selected here.
/// </summary>
public static class WindowIconTheme
{
    private const string WhiteIconSourceRelativePath = "Assets\\WindowIcon-ForDarkTheme.ico";
    private const string BlackIconSourceRelativePath = "Assets\\WindowIcon-ForLightTheme.ico";
    private const string WhiteLogoSourceUri = "ms-appx:///Assets/Icon-Small-Dark-256.png";
    private const string BlackLogoSourceUri = "ms-appx:///Assets/Icon-Small-Light-256.png";

    public const string IconForDarkThemeRelativePath = WhiteIconSourceRelativePath;
    public const string IconForLightThemeRelativePath = BlackIconSourceRelativePath;
    public const string LogoForDarkThemeUri = WhiteLogoSourceUri;
    public const string LogoForLightThemeUri = BlackLogoSourceUri;

    public static AppTheme Resolve(string? preference, bool systemUsesLightTheme) =>
        SettingsService.NormalizeTheme(preference) switch
        {
            AppTheme.Light => AppTheme.Light,
            AppTheme.Dark => AppTheme.Dark,
            _ => systemUsesLightTheme ? AppTheme.Light : AppTheme.Dark
        };

    public static string GetIconRelativePath(AppTheme resolvedTheme) =>
        resolvedTheme == AppTheme.Dark ? IconForDarkThemeRelativePath : IconForLightThemeRelativePath;

    public static string GetLogoUri(AppTheme resolvedTheme) =>
        resolvedTheme == AppTheme.Dark ? LogoForDarkThemeUri : LogoForLightThemeUri;
}
