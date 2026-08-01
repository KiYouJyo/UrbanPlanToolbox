using Microsoft.UI.Xaml;

namespace UrbanPlanToolbox.Services;

/// <summary>Maps persisted settings to the root visual's requested theme.</summary>
public static class ThemePreference
{
    public static ElementTheme ToElementTheme(string? theme) => theme switch
    {
        "Light" => ElementTheme.Light,
        "Dark" => ElementTheme.Dark,
        _ => ElementTheme.Default
    };

    public static void Apply(FrameworkElement? root, string? theme)
    {
        if (root is not null) root.RequestedTheme = ToElementTheme(theme);
    }
}
