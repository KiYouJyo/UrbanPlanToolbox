using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class ProjectWorkspaceRepairContractTests
{
    [Fact]
    public void DesignOverviewUsesKeyStrategiesInsteadOfAmbiguousCurrentStage()
    {
        var root = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "Views", "ProjectWorkspacePage.xaml"));
        var fixes = File.ReadAllText(Path.Combine(root, "Views", "ProjectWorkspacePage.Round1Fixes.cs"));
        var polish = File.ReadAllText(Path.Combine(root, "Views", "ProjectWorkspacePage.Round6.UiPolish.cs"));

        Assert.Contains("Loaded=\"OnRound6WorkspaceLoaded\"", xaml);
        Assert.Contains("OnRound1WorkspaceLoaded(sender, e);", polish);
        Assert.Contains("OnWorkspaceLoaded(sender, e);", fixes);
        Assert.Contains("EditOverviewButton.Click += OnRound1EditOverview", fixes);
        Assert.Contains("var label = W(\"重点策略\"", fixes);
        Assert.Contains("ProjectStrategyList.Count(_project.PlanningRequirements)", fixes);
        Assert.DoesNotContain("OverviewLabel2.Text = W(\"当前阶段\"", fixes);
    }

    [Fact]
    public void WorkspaceLayoutRepairIsIdempotentDuringLayoutUpdated()
    {
        var root = FindRepositoryRoot();
        var fixes = File.ReadAllText(Path.Combine(root, "Views", "ProjectWorkspacePage.Round1Fixes.cs"));

        Assert.Contains("TileCanvas.LayoutUpdated -= OnRound1CanvasLayoutUpdated", fixes);
        Assert.Contains("TileCanvas.LayoutUpdated += OnRound1CanvasLayoutUpdated", fixes);
        Assert.Contains("if (!string.Equals(OverviewLabel2.Text, label, StringComparison.Ordinal))", fixes);
        Assert.Contains("if (!string.Equals(OverviewValue2.Text, value, StringComparison.Ordinal))", fixes);
        Assert.DoesNotContain("RewireRound1OverviewEditor();\n            ApplyRound1OverviewMetrics();", fixes);
    }

    [Fact]
    public void WorkspaceCardEditorsSaveOnEscapeEvenWhenTextInputHasFocus()
    {
        var root = FindRepositoryRoot();
        var fixes = File.ReadAllText(Path.Combine(root, "Views", "ProjectWorkspacePage.Round1Fixes.cs"));

        Assert.Contains("UIElement.KeyDownEvent", fixes);
        Assert.Contains("new KeyboardAccelerator { Key = VirtualKey.Escape }", fixes);
        Assert.Contains("escape.Invoked += OnRound1EscapeAcceleratorInvoked", fixes);
        Assert.Contains("CompleteRound1DrawerEdit();", fixes);
        Assert.Contains("Project_Action_Save", fixes);
        Assert.Contains("ButtonAutomationPeer", fixes);
        Assert.Contains("IInvokeProvider", fixes);
        Assert.Contains("invokeProvider.Invoke();", fixes);
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
        Assert.Contains("OpenRound1StrategyEditor", fixes);
        Assert.Contains("ProjectStrategyList.Serialize", fixes);
        Assert.Contains("['\\r', '\\n', '；', ';']", strategyList);
        Assert.Contains("StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries", strategyList);
    }

    [Fact]
    public void KeyStrategyCardWrapsEveryItemAndScrollsVertically()
    {
        var root = FindRepositoryRoot();
        var fixes = File.ReadAllText(Path.Combine(root, "Views", "ProjectWorkspacePage.Round1Fixes.cs"));
        var start = fixes.IndexOf("private UIElement BuildRound1Strategies", StringComparison.Ordinal);
        var end = fixes.IndexOf("private FlyoutBase CreateRound1StrategyMenu", start, StringComparison.Ordinal);
        var renderer = fixes[start..end];

        Assert.Contains("ProjectStrategyList.Parse", renderer);
        Assert.Contains("new Grid", renderer);
        Assert.Contains("GridUnitType.Star", renderer);
        Assert.Contains("TextWrapping = TextWrapping.Wrap", renderer);
        Assert.Contains("VerticalScrollMode = ScrollMode.Enabled", renderer);
        Assert.Contains("VerticalScrollBarVisibility = ScrollBarVisibility.Auto", renderer);
        Assert.Contains("HorizontalScrollMode = ScrollMode.Disabled", renderer);
        Assert.Contains("HorizontalContentAlignment = HorizontalAlignment.Stretch", renderer);
        Assert.DoesNotContain(".Take(", renderer);
        Assert.DoesNotContain("TextTrimming", renderer);
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
        Assert.DoesNotContain("ProjectWorkspacePanelKinds.Literature", researchKinds);
        Assert.DoesNotContain("ProjectWorkspacePanelKinds.TextNote", researchKinds);
        Assert.DoesNotContain("ProjectWorkspacePanelKinds.Custom", researchKinds);

        var designKindsStart = service.IndexOf("private static readonly string[] DesignKinds", StringComparison.Ordinal);
        var designKindsEnd = service.IndexOf("private static readonly string[] ResearchKinds", designKindsStart, StringComparison.Ordinal);
        var designKinds = service[designKindsStart..designKindsEnd];
        Assert.DoesNotContain("ProjectWorkspacePanelKinds.TextNote", designKinds);
        Assert.DoesNotContain("ProjectWorkspacePanelKinds.Custom", designKinds);

        var researchDefaultStart = service.IndexOf("if (string.Equals(projectKind, ProjectKindCodes.Research", StringComparison.Ordinal);
        var designDefaultStart = service.IndexOf("else", researchDefaultStart, StringComparison.Ordinal);
        var researchDefault = service[researchDefaultStart..designDefaultStart];
        Assert.DoesNotContain("ProjectWorkspacePanelKinds.Chart", researchDefault);
        Assert.DoesNotContain("ProjectWorkspacePanelKinds.DataAndScripts", researchDefault);
        Assert.DoesNotContain("ProjectWorkspacePanelKinds.Literature", researchDefault);
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
            var hasProject = File.Exists(Path.Combine(directory.FullName, "UrbanPlanToolbox.csproj"));
            var hasViews = File.Exists(Path.Combine(directory.FullName, "Views", "ProjectWorkspacePage.xaml"));
            var hasServices = Directory.Exists(Path.Combine(directory.FullName, "Services"));
            if (hasProject && hasViews && hasServices) return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository source root could not be located.");
    }
}
