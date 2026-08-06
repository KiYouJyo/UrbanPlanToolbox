using UrbanPlanToolbox.Services;
using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class RuntimeLanguageSwitchingTests
{
    [Fact]
    public void RuntimeLanguageResourcesExistInEveryCatalog()
    {
        foreach (var language in ReswCatalog.Languages)
        {
            var resources = ReswCatalog.Load(language);
            Assert.True(resources.ContainsKey("Settings_LanguageDescription_Runtime"));
            Assert.False(string.IsNullOrWhiteSpace(resources["Settings_LanguageDescription_Runtime"]));
            Assert.True(resources.ContainsKey("Setting_Language_SwitchFailed"));
        }
    }

    [Fact]
    public void NavigationStateUsesStableValuesAndRestoresWithoutPageInstances()
    {
        var service = new NavigationStateService();
        var state = new ShellNavigationState("design-tools", "UrbanPlanToolbox.Views.DesignToolsPage", false);

        service.Save(state);

        Assert.Equal(state, service.Restore());
        Assert.DoesNotContain("Page", service.Restore()!.PrimaryNavigationId, StringComparison.Ordinal);
        Assert.Equal("UrbanPlanToolbox.Views.DesignToolsPage", service.Restore()!.PageTypeName);
        Assert.IsType<ShellNavigationState>(service.Restore());
    }

    [Fact]
    public void LocalizationContractExposesSingleRuntimeSwitchEntryPoint()
    {
        var sourceRoot = FindRepositoryRoot();
        var contract = File.ReadAllText(Path.Combine(sourceRoot, "Services", "ILocalizationService.cs"));
        var settings = File.ReadAllText(Path.Combine(sourceRoot, "Views", "SettingsPage.xaml.cs"));

        Assert.Contains("SwitchLanguageAsync", contract);
        Assert.Contains("LanguageChanged", contract);
        Assert.Contains("SwitchLanguageAsync", settings);
        Assert.DoesNotContain("PrimaryLanguageOverride", settings);
        Assert.DoesNotContain("ShowLanguageRestartDialogAsync", settings);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "UrbanPlanToolbox.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
