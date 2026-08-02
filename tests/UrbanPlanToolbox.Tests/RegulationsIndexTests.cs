using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Models.Tools;
using UrbanPlanToolbox.Services;
using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class RegulationsIndexTests
{
    [Fact]
    public void PackagedSchemaContractUsesIndependentVersion()
    {
        var data = CreateData();
        var service = new RegulationsIndexService(data);
        Assert.Equal(1, service.Data.DataVersion);
        Assert.Single(service.Search("scope"));
        Assert.Single(service.Search("scope", region: "中国"));
    }

    [Fact]
    public void RegulationToolUsesOneStableDualPlacement()
    {
        var tool = ToolRegistry.Default.GetById(ToolIds.RegulationsIndex);
        Assert.Equal("architecture-planning-regulations-index", tool.Id);
        Assert.Equal(2, tool.GetPlacements().Count);
        Assert.Contains(tool.GetPlacements(), placement => placement.PrimaryCategory == ToolPrimaryCategory.Design && placement.SecondaryCategory == ToolSecondaryCategory.MasterPlanning);
        Assert.Contains(tool.GetPlacements(), placement => placement.PrimaryCategory == ToolPrimaryCategory.Research && placement.SecondaryCategory == ToolSecondaryCategory.ResearchPreparation);
    }

    [Theory]
    [InlineData("https://example.com", true)]
    [InlineData(" http://example.com/path ", true)]
    [InlineData("file:///C:/secret.txt", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("not a uri", false)]
    public void ExternalLinksOnlyAllowTrimmedHttpAndHttps(string value, bool expected)
    {
        Assert.Equal(expected, ExternalLinkService.IsSafeHttpUri(value, out var uri));
        if (expected) Assert.NotNull(uri); else Assert.Null(uri);
    }

    [Fact]
    public void PackagedJsonSnapshotDeserializesAndPassesRuntimeValidation()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "Assets", "Data", "RegulationsIndex"))) root = root.Parent;
        var path = Path.Combine(root?.FullName ?? string.Empty, "Assets", "Data", "RegulationsIndex", RegulationsIndexService.DataFileName);
        Assert.True(File.Exists(path), path);
        var data = RegulationsIndexService.Deserialize(File.ReadAllText(path));
        Assert.Equal(1, data.DataVersion);
        Assert.Equal(221, data.Entries.Count);
        Assert.Equal(20, data.OfficialPortals.Count);
    }

    [Fact]
    public void RegulationsPageUsesVirtualizedListTemplatesAndTriLanguageLinkResources()
    {
        var root = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "Views", "RegulationsIndexPage.xaml"));
        Assert.Contains("<ListView x:Name=\"EntriesList\"", xaml);
        Assert.Contains("<ListView.ItemTemplate>", xaml);
        Assert.Contains("IsTabStop\" Value=\"False\"", xaml);
        Assert.Contains("UseSystemFocusVisuals\" Value=\"False\"", xaml);
        Assert.Contains("FocusVisualMargin=\"0\"", xaml);
        Assert.Contains("FocusVisualSecondaryThickness=\"0\"", xaml);
        Assert.Contains("TextWrapping=\"Wrap\"", xaml);
        Assert.Contains("<ListView x:Name=\"PortalsList\"", xaml);
        foreach (var language in new[] { "zh-CN", "ja-JP", "en-US" })
        {
            var resources = File.ReadAllText(Path.Combine(root, "Strings", language, "Resources.resw"));
            Assert.Contains("Regulations_OpenOfficial", resources);
            Assert.Contains("Regulations_OpenFailed", resources);
        }
    }

    [Fact]
    public void ToolPagesDoNotDuplicatePageLevelFavorites()
    {
        var root = FindRepositoryRoot();
        foreach (var page in new[]
        {
            "Views/RegulationsIndexPage.xaml",
            "Views/PlanningCalculatorPage.xaml",
            "Views/UnitScaleConverterPage.xaml"
        })
        {
            var xaml = File.ReadAllText(Path.Combine(root, page));
            Assert.DoesNotContain("FavoriteButton", xaml);
        }

        var toolCards = File.ReadAllText(Path.Combine(root, "Controls", "ToolCardsView.xaml"));
        Assert.Contains("FavoriteButton", toolCards);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && (!File.Exists(Path.Combine(directory.FullName, "UrbanPlanToolbox.csproj")) || !Directory.Exists(Path.Combine(directory.FullName, "Views")))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }

    private static RegulationsIndexDocument CreateData() => new()
    {
        DataVersion = 1,
        SourceVerifiedDate = "2026-07-31",
        Entries = Enumerable.Range(1, 221).Select(id => new RegulationEntry { Id = id, Region = id == 1 ? "中国" : "日本", OriginalTitle = $"title-{id}", ScopeAndPurpose = id == 1 ? "scope" : "other", VerifiedDate = "2026-07-31", OfficialUrl = "https://example.com/legal" }).ToList(),
        OfficialPortals = Enumerable.Range(1, 20).Select(id => new OfficialPortal { PortalId = $"portal-{id}", Region = "中国", PlatformName = "Portal", Url = "https://example.com/portal" }).ToList()
    };
}
