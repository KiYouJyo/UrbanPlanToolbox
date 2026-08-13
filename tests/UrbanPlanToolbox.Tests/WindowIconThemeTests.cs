using UrbanPlanToolbox.Services;
using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class WindowIconThemeTests
{
    [Theory]
    [InlineData("Dark", true, AppTheme.Dark, WindowIconTheme.IconForDarkThemeRelativePath, WindowIconTheme.LogoForDarkThemeUri)]
    [InlineData("Light", false, AppTheme.Light, WindowIconTheme.IconForLightThemeRelativePath, WindowIconTheme.LogoForLightThemeUri)]
    [InlineData("System", false, AppTheme.Dark, WindowIconTheme.IconForDarkThemeRelativePath, WindowIconTheme.LogoForDarkThemeUri)]
    [InlineData("System", true, AppTheme.Light, WindowIconTheme.IconForLightThemeRelativePath, WindowIconTheme.LogoForLightThemeUri)]
    public void AppWindowChromeUsesTheResolvedApplicationTheme(string preference, bool systemUsesLightTheme, AppTheme expected, string expectedIcon, string expectedLogo)
    {
        var resolved = WindowIconTheme.Resolve(preference, systemUsesLightTheme);

        Assert.Equal(expected, resolved);
        Assert.Equal(expectedIcon, WindowIconTheme.GetIconRelativePath(resolved));
        Assert.Equal(expectedLogo, WindowIconTheme.GetLogoUri(resolved));
    }

    [Fact]
    public void ReversedWindowMappingsAreNotRepresentable()
    {
        Assert.NotEqual(WindowIconTheme.IconForLightThemeRelativePath, WindowIconTheme.GetIconRelativePath(AppTheme.Dark));
        Assert.NotEqual(WindowIconTheme.IconForDarkThemeRelativePath, WindowIconTheme.GetIconRelativePath(AppTheme.Light));
    }

    [Fact]
    public void MixedAppAndShellThemesUseIndependentAssetSelections()
    {
        // Windows Light + App Dark: title bar is white, Shell resolves its black theme-light candidate.
        Assert.Equal(WindowIconTheme.IconForDarkThemeRelativePath, WindowIconTheme.GetIconRelativePath(AppTheme.Dark));
        Assert.Equal(WindowIconTheme.LogoForDarkThemeUri, WindowIconTheme.GetLogoUri(AppTheme.Dark));
        Assert.NotEqual(WindowIconTheme.IconForDarkThemeRelativePath, WindowIconTheme.IconForLightThemeRelativePath);

        // Windows Dark + App Light: title bar is black, Shell resolves its white default candidate.
        Assert.Equal(WindowIconTheme.IconForLightThemeRelativePath, WindowIconTheme.GetIconRelativePath(AppTheme.Light));
        Assert.Equal(WindowIconTheme.LogoForLightThemeUri, WindowIconTheme.GetLogoUri(AppTheme.Light));
        Assert.NotEqual(WindowIconTheme.LogoForDarkThemeUri, WindowIconTheme.LogoForLightThemeUri);
    }
}
