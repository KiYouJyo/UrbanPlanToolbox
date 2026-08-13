using UrbanPlanToolbox.Services;
using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class StartupSplashTests
{
    [Theory]
    [InlineData(50, 500)]
    [InlineData(300, 500)]
    [InlineData(500, 500)]
    [InlineData(900, 900)]
    public void VisibleDurationIsTheMaximumOfMinimumAndInitialization(int initializationMilliseconds, int expectedMilliseconds)
    {
        var duration = StartupSplashTiming.ResolveVisibleDuration(TimeSpan.FromMilliseconds(initializationMilliseconds));
        Assert.Equal(TimeSpan.FromMilliseconds(expectedMilliseconds), duration);
    }

    [Fact]
    public void FadeOutUsesTheSpecifiedShortDurationWithASeparateFailOpenDeadline()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(200), StartupSplashTiming.FadeOutDuration);
        Assert.InRange(StartupSplashTiming.FadeOutDuration, TimeSpan.FromMilliseconds(180), TimeSpan.FromMilliseconds(220));
        Assert.True(StartupSplashTiming.FadeOutFallbackDuration > StartupSplashTiming.FadeOutDuration);
    }

    [Theory]
    [InlineData("Dark", true, StartupSplashTheme.Dark, StartupSplashPresentation.LogoForDarkThemeAssetUri)]
    [InlineData("Light", false, StartupSplashTheme.Light, StartupSplashPresentation.LogoForLightThemeAssetUri)]
    [InlineData("System", false, StartupSplashTheme.Dark, StartupSplashPresentation.LogoForDarkThemeAssetUri)]
    [InlineData("System", true, StartupSplashTheme.Light, StartupSplashPresentation.LogoForLightThemeAssetUri)]
    public void ThemeSelectionUsesPersistedPreferenceOrSystemFallback(string preference, bool systemUsesLightTheme, StartupSplashTheme expectedTheme, string expectedAsset)
    {
        var theme = StartupSplashPresentation.ResolveTheme(preference, systemUsesLightTheme);
        Assert.Equal(expectedTheme, theme);
        Assert.Equal(expectedAsset, StartupSplashPresentation.GetLogoAssetUri(theme));
    }

    [Fact]
    public void ThemeAssetsUseTheMatchingLargeLogoPair()
    {
        Assert.Equal("ms-appx:///Assets/Icon-Large-Dark-1024.png", StartupSplashPresentation.LogoForDarkThemeAssetUri);
        Assert.Equal("ms-appx:///Assets/Icon-Large-Light-1024.png", StartupSplashPresentation.LogoForLightThemeAssetUri);
    }
}
