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

    private static RegulationsIndexDocument CreateData() => new()
    {
        DataVersion = 1,
        SourceVerifiedDate = "2026-07-31",
        Entries = Enumerable.Range(1, 221).Select(id => new RegulationEntry { Id = id, Region = id == 1 ? "中国" : "日本", OriginalTitle = $"title-{id}", ScopeAndPurpose = id == 1 ? "scope" : "other", VerifiedDate = "2026-07-31", OfficialUrl = "https://example.com/legal" }).ToList(),
        OfficialPortals = Enumerable.Range(1, 20).Select(id => new OfficialPortal { PortalId = $"portal-{id}", Region = "中国", PlatformName = "Portal", Url = "https://example.com/portal" }).ToList()
    };
}
