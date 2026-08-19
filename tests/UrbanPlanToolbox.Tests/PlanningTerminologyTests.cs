using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Services;
using System.Xml.Linq;
using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class PlanningTerminologyTests
{
    private readonly PlanningTerminologyService _service = PlanningTerminologyService.LoadPackaged();

    [Fact]
    public void LegacyBundledDatasetLoadsAndCountsValidate() => Assert.Equal((1, "1.0.0", 140, 266, 198, 24, 31), (_service.Dataset.SchemaVersion, _service.Dataset.DataVersion, _service.Dataset.Counts.Terms, _service.Dataset.Counts.Aliases, _service.Dataset.Counts.Relations, _service.Dataset.Counts.HighRisk, _service.Dataset.Counts.Sources));

    [Fact]
    public void RepresentativeTermsRemainIntact()
    {
        Assert.Equal("城镇开发边界", _service.GetTerm(20)!.ZhCN);
        Assert.Equal("市街化区域", _service.GetTerm(37)!.ZhCN);
        Assert.Equal("市街化調整区域", _service.GetTerm(38)!.JaJP);
        Assert.Equal("容積率", _service.GetTerm(65)!.JaJP);
        Assert.Contains("TOD", _service.GetTerm(79)!.Aliases);
        Assert.Contains("GWR", _service.GetTerm(108)!.Aliases);
        Assert.Equal("主体功能分区", _service.GetTerm(126)!.ZhCN);
        Assert.Equal("居住調整地域", _service.GetTerm(136)!.JaJP);
    }

    [Theory]
    [InlineData("城镇开发边界", 20)] [InlineData("市街化区域", 37)] [InlineData("Urbanization Promotion Area", 37)] [InlineData("控规", 27)] [InlineData("TOD", 79)] [InlineData("GWR", 108)] [InlineData("ようせきりつ", 65)] [InlineData("FAR", 65)] [InlineData("HousingControlArea", 136)] [InlineData("floor area ratio", 65)]
    public void LegacySearchSupportsPrimaryAliasAndReading(string query, int expectedId) => Assert.Equal(expectedId, _service.Search(query).First().Term.Id);

    [Fact]
    public void RelationsAndHighRiskComparisonsResolve()
    {
        Assert.Contains(_service.GetRelatedTerms(20), item => item.Term.Id == 37 && item.Relation.RelationType == "approximate-equivalent");
        Assert.Contains(_service.GetRelatedTerms(20), item => item.Term.Id == 38 && item.Relation.RelationType == "approximate-equivalent");
        Assert.Contains(_service.GetRelatedTerms(136), item => item.Term.Id == 38 && item.Relation.RelationType == "not-equivalent");
        Assert.NotEmpty(_service.GetHighRiskEquivalences(20));
    }

    [Fact]
    public void SourcesResolveAndMissingSourceFailsValidation()
    {
        Assert.Contains(_service.GetSources(_service.GetTerm(134)!), source => source.Status.Contains("官方"));
        var invalid = new PlanningTerminologyDataset { SchemaVersion = 1, DataVersion = "1.0.0", Sources = new() { ["S1"] = new() }, Terms = [new PlanningTerm { Id = 1, Equivalence = "exact", SourceIds = ["MISSING"] }], Counts = new PlanningTerminologyCounts { Terms = 1, Sources = 1 } };
        Assert.Throws<InvalidDataException>(() => PlanningTerminologyService.Validate(invalid));
    }

    [Fact]
    public void EveryRelationTypeHasLocalizedLabelsInAllLanguages()
    {
        var relationTypes = _service.Dataset.Relations.Select(relation => relation.RelationType).Distinct(StringComparer.Ordinal).ToArray();
        foreach (var language in new[] { "zh-CN", "ja-JP", "en-US" })
        {
            var resourcePath = FindRepoFile(Path.Combine("Strings", language, "Resources.resw"));
            var keys = XDocument.Load(resourcePath).Descendants("data").Select(data => (string?)data.Attribute("name")).OfType<string>().ToHashSet(StringComparer.Ordinal);
            Assert.All(relationTypes, relationType => Assert.Contains($"Terminology_Relation_{relationType}", keys));
        }
    }

    [Fact]
    public void TerminologyResourceKeySetsMatchAcrossLanguages()
    {
        HashSet<string>? baseline = null;
        foreach (var language in new[] { "zh-CN", "ja-JP", "en-US" })
        {
            var resourcePath = FindRepoFile(Path.Combine("Strings", language, "Resources.resw"));
            var keys = XDocument.Load(resourcePath).Descendants("data").Select(data => (string?)data.Attribute("name")).OfType<string>().Where(name => name.StartsWith("Terminology_", StringComparison.Ordinal)).ToHashSet(StringComparer.Ordinal);
            baseline ??= keys;
            Assert.True(baseline.SetEquals(keys), $"Terminology resource keys differ for {language}.");
        }
    }

    [Fact]
    public void WarningBannerFallbackAlwaysHasMessage()
    {
        var warningTerms = _service.Dataset.Terms.Where(term => term.ReleaseStatus.Contains("警告", StringComparison.Ordinal) || term.ReleaseStatus.Contains("标准化", StringComparison.Ordinal)).ToArray();
        Assert.NotEmpty(warningTerms);
        Assert.All(warningTerms, term =>
        {
            var message = string.IsNullOrWhiteSpace(term.ReviewNote) ? "制度或标准化提示" : term.ReviewNote;
            Assert.False(string.IsNullOrWhiteSpace(message));
        });
    }

    [Fact]
    public void TerminologyPageUsesFigmaDataPackSourceAndTwoPaneLayout()
    {
        var xaml = File.ReadAllText(FindRepoFile(Path.Combine("Views", "PlanningTerminologyPage.xaml")));
        var code = File.ReadAllText(FindRepoFile(Path.Combine("Views", "PlanningTerminologyPage.xaml.cs")));

        Assert.Contains("x:Name=\"BackButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"CurrentSourceLabel\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"HeaderCheckButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ManageButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"TermsList\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"DetailPanel\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"JurisdictionBox\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"CategoryBox\"", xaml, StringComparison.Ordinal);
        Assert.Contains("<ListView.ItemTemplate>", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsWrapGrid", xaml, StringComparison.Ordinal);

        Assert.Contains("ReferenceDataPackIds.PlanningTerminology", code, StringComparison.Ordinal);
        Assert.Contains("ReferenceDataPackService.Default.LoadActiveAsync(PackId)", code, StringComparison.Ordinal);
        Assert.Contains("ReferenceDataPackPageCoordinator.CheckAndInstallUpdateAsync", code, StringComparison.Ordinal);
        Assert.Contains("ReferenceDataPackPageCoordinator.ManageAsync", code, StringComparison.Ordinal);
        Assert.Contains("BuildAliasesByTerm", code, StringComparison.Ordinal);
        Assert.Contains("HighRiskEquivalences", code, StringComparison.Ordinal);
        Assert.Contains("RelatedTermIds", code, StringComparison.Ordinal);
        Assert.DoesNotContain("PlanningTerminologyService.LoadPackaged", code, StringComparison.Ordinal);
    }

    [Fact]
    public void TerminologyPageStacksListAndDetailForNarrowLayouts()
    {
        var xaml = File.ReadAllText(FindRepoFile(Path.Combine("Views", "PlanningTerminologyPage.xaml")));
        var code = File.ReadAllText(FindRepoFile(Path.Combine("Views", "PlanningTerminologyPage.xaml.cs")));
        Assert.Contains("x:Name=\"ContentGrid\"", xaml, StringComparison.Ordinal);
        Assert.Contains("<RowDefinition Height=\"*\"/><RowDefinition Height=\"0\"/>", xaml, StringComparison.Ordinal);
        Assert.Contains("Grid.SetRow(ListPanel, 0)", code, StringComparison.Ordinal);
        Assert.Contains("Grid.SetRow(DetailPanel, 1)", code, StringComparison.Ordinal);
        Assert.Contains("Grid.SetColumnSpan(DetailPanel, 2)", code, StringComparison.Ordinal);
        Assert.Contains("e.NewSize.Width < 900", code, StringComparison.Ordinal);
    }

    private static string FindRepoFile(string relativePath)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException(relativePath);
    }
}
