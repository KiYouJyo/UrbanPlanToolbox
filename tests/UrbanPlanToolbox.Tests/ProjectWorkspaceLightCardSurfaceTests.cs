using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class ProjectWorkspaceLightCardSurfaceTests
{
    [Fact]
    public void LightWorkspaceCardsUseTheSameFirstLevelStyleAsAboutAndSettingsWhileDarkPathStaysFrozen()
    {
        var root = FindRepositoryRoot();
        var workspaceXaml = File.ReadAllText(Path.Combine(root, "Views", "ProjectWorkspacePage.xaml"));
        var aboutXaml = File.ReadAllText(Path.Combine(root, "Views", "AboutPage.xaml"));
        var settingsXaml = File.ReadAllText(Path.Combine(root, "Views", "SettingsPage.xaml"));
        var polish = File.ReadAllText(Path.Combine(root, "Views", "ProjectWorkspacePage.Round6.UiPolish.cs"));

        Assert.Contains("x:Name=\"OverviewCard\" Grid.Row=\"2\" Style=\"{StaticResource SettingsSectionCardStyle}\"", workspaceXaml);
        Assert.Contains("Style=\"{StaticResource SettingsSectionCardStyle}\"", aboutXaml);
        Assert.Contains("Style=\"{StaticResource SettingsSectionCardStyle}\"", settingsXaml);

        Assert.Contains("if (ActualTheme != ElementTheme.Light)", polish);
        Assert.Contains("OverviewCard.Background = background", polish);
        Assert.Contains("tile.Background = background", polish);
        Assert.Contains("OverviewCard.ClearValue(Border.BackgroundProperty)", polish);
        Assert.Contains("Application.Current.Resources[\"SettingsSectionCardStyle\"]", polish);
        Assert.Contains("tile.Style = firstLevelStyle", polish);
        Assert.Contains("tile.ClearValue(Border.BackgroundProperty)", polish);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "UrbanPlanToolbox.csproj")) &&
                Directory.Exists(Path.Combine(directory.FullName, "Views")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository source root could not be located.");
    }
}
