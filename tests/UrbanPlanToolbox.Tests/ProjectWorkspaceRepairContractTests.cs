using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class ProjectWorkspaceRepairContractTests
{
    [Fact]
    public void DesignOverviewUsesKeyStrategiesInsteadOfAmbiguousCurrentStage()
    {
        var root = FindRepositoryRoot();
        var fixes = File.ReadAllText(Path.Combine(root, "Views", "ProjectWorkspacePage.Round1Fixes.cs"));

        Assert.Contains("OverviewLabel2.Text = W(\"重点策略\"", fixes);
        Assert.Contains("ProjectStrategyList.Count(_project.PlanningRequirements)", fixes);
        Assert.DoesNotContain("OverviewLabel2.Text = W(\"当前阶段\"", fixes);
    }

    [Fact]
    public void WorkspaceCardEditorsCloseOnEscape()
    {
        var root = FindRepositoryRoot();
        var fixes = File.ReadAllText(Path.Combine(root, "Views", "ProjectWorkspacePage.Round1Fixes.cs"));

        Assert.Contains("UIElement.KeyDownEvent", fixes);
        Assert.Contains("VirtualKey.Escape", fixes);
        Assert.Contains("CloseDrawer();", fixes);
        Assert.Contains("e.Handled = true;", fixes);
    }

    [Fact]
    public void KeyStrategiesUseARepeatableRowEditorAndLegacyTextRemainsReadable()
    {
        var root = FindRepositoryRoot();
        var fixes = File.ReadAllText(Path.Combine(root, "Views", "ProjectWorkspacePage.Round1Fixes.cs"));
        var strategyList = File.ReadAllText(Path.Combine(root, "Services", "ProjectStrategyList.cs"));

        Assert.Contains("StrategyListEditor", fixes);
        Assert.Contains("AddRow", fixes);
        Assert.Contains("RemoveRow", fixes);
        Assert.Contains("ProjectStrategyList.Serialize", fixes);
        Assert.Contains("['\\r', '\\n', '；', ';']", strategyList);
        Assert.Contains("StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries", strategyList);
    }

    [Fact]
    public void ResearchWorkspaceDoesNotOfferChartOrDataAndScriptsCards()
    {
        var root = FindRepositoryRoot();
        var service = File.ReadAllText(Path.Combine(root, "Services", "ProjectWorkspaceLayoutService.cs"));
        var researchKindsStart = service.IndexOf("private static readonly string[] ResearchKinds", StringComparison.Ordinal);
        var researchKindsEnd = service.IndexOf("public static IReadOnlyList<string> GetAllowedPanelKinds", researchKindsStart, StringComparison.Ordinal);
        var researchKinds = service[researchKindsStart..researchKindsEnd];

        Assert.DoesNotContain("ProjectWorkspacePanelKinds.Chart", researchKinds);
        Assert.DoesNotContain("ProjectWorkspacePanelKinds.DataAndScripts", researchKinds);

        var researchDefaultStart = service.IndexOf("if (string.Equals(projectKind, ProjectKindCodes.Research", StringComparison.Ordinal);
        var designDefaultStart = service.IndexOf("else", researchDefaultStart, StringComparison.Ordinal);
        var researchDefault = service[researchDefaultStart..designDefaultStart];
        Assert.DoesNotContain("ProjectWorkspacePanelKinds.Chart", researchDefault);
        Assert.DoesNotContain("ProjectWorkspacePanelKinds.DataAndScripts", researchDefault);
    }

    [Fact]
    public void PrivacyAndThirdPartyNoticesUseFormattedMarkdownPresentation()
    {
        var root = FindRepositoryRoot();
        var about = File.ReadAllText(Path.Combine(root, "Views", "AboutPage.Markdown.cs"));
        var renderer = File.ReadAllText(Path.Combine(root, "Views", "MarkdownDocumentView.cs"));
        var parser = File.ReadAllText(Path.Combine(root, "Services", "SimpleMarkdownParser.cs"));

        Assert.Contains("PRIVACY.md", about);
        Assert.Contains("THIRD-PARTY-NOTICES.md", about);
        Assert.Contains("MarkdownDocumentView.Build(markdown)", about);
        Assert.Contains("SimpleMarkdownParser.Parse(markdown)", renderer);
        Assert.Contains("SimpleMarkdownBlockKind.Heading1", renderer);
        Assert.Contains("SimpleMarkdownBlockKind.UnorderedListItem", renderer);
        Assert.Contains(".Replace(\"`\", string.Empty", parser);
        Assert.DoesNotContain("Title = fileName", about);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "UrbanPlanToolbox.csproj"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root could not be located.");
    }
}
