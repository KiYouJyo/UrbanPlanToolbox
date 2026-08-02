using System.Text.RegularExpressions;
using System.Xml.Linq;
using UrbanPlanToolbox.Models.Navigation;
using UrbanPlanToolbox.Models.Tools;
using UrbanPlanToolbox.Services;
using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed partial class LocalizationTests
{
    [Fact]
    public void ThreeResourceFilesHaveIdenticalKeySets()
    {
        var baseKeys = ReswCatalog.Load("zh-CN").Keys.ToHashSet(StringComparer.Ordinal);
        foreach (var language in ReswCatalog.Languages)
        {
            Assert.Equal(baseKeys, ReswCatalog.Load(language).Keys.ToHashSet(StringComparer.Ordinal));
        }
    }

    [Fact]
    public void ZhCnDefaultResourcesHaveNoEmptyValues()
    {
        Assert.All(ReswCatalog.Load("zh-CN"), pair => Assert.False(string.IsNullOrWhiteSpace(pair.Value)));
    }

    [Fact]
    public void JaAndEnResourcesHaveNoUnexpectedEmptyValues()
    {
        foreach (var language in new[] { "ja-JP", "en-US" })
        {
            Assert.All(ReswCatalog.Load(language), pair => Assert.False(string.IsNullOrWhiteSpace(pair.Value)));
        }
    }

    [Fact]
    public void NoDuplicateResourceKeys()
    {
        foreach (var language in ReswCatalog.Languages)
        {
            var keys = ReswCatalog.Load(language).Keys;
            Assert.Equal(keys.Count(), keys.Distinct(StringComparer.Ordinal).Count());
        }
    }

    [Fact]
    public void FormatStringPlaceholdersAreConsistentAcrossLanguages()
    {
        var zh = ReswCatalog.Load("zh-CN");
        foreach (var (key, zhValue) in zh)
        {
            var zhPlaceholders = ExtractPlaceholders(zhValue);
            foreach (var language in new[] { "ja-JP", "en-US" })
            {
                Assert.Equal(zhPlaceholders, ExtractPlaceholders(ReswCatalog.Load(language)[key]));
            }

            if (zhPlaceholders.Count > 0)
            {
                Assert.Equal(
                    Enumerable.Range(0, zhPlaceholders.Max() + 1).ToArray(),
                    zhPlaceholders.OrderBy(number => number).ToArray());
            }
        }
    }

    [Fact]
    public void ToolDefinitionsReferenceExistingResourceKeys()
    {
        foreach (var language in ReswCatalog.Languages)
        {
            var resources = ReswCatalog.Load(language);
            foreach (var tool in ToolRegistry.Default.All)
            {
                Assert.True(resources.ContainsKey(tool.NameResourceKey));
                Assert.True(resources.ContainsKey(tool.DescriptionResourceKey));
                Assert.True(resources.ContainsKey(tool.SearchKeywordsResourceKey));
            }
        }
    }

    [Fact]
    public void CategoryDefinitionsReferenceExistingResourceKeys()
    {
        foreach (var language in ReswCatalog.Languages)
        {
            var resources = ReswCatalog.Load(language);
            foreach (var category in ToolCategoryCatalog.Design.Concat(ToolCategoryCatalog.Research))
            {
                Assert.True(resources.ContainsKey(category.NameResourceKey));
            }

            foreach (var primary in new[] { ToolPrimaryCategory.Design, ToolPrimaryCategory.Research })
            {
                Assert.True(resources.ContainsKey(primary.GetNameResourceKey()));
            }
        }
    }

    [Fact]
    public void PrimaryNavigationResourceKeysExist()
    {
        foreach (var language in ReswCatalog.Languages)
        {
            var resources = ReswCatalog.Load(language);
            foreach (var route in PrimaryNavigation.Default.All)
            {
                Assert.True(resources.ContainsKey(route.NameResourceKey));
            }

            Assert.True(resources.ContainsKey("Navigation_Settings"));
        }
    }

    [Theory]
    [InlineData("system", null)]
    [InlineData("System", null)]
    [InlineData("zh-CN", "zh-CN")]
    [InlineData("ja-JP", "ja-JP")]
    [InlineData("en-US", "en-US")]
    public void LanguagePreferenceMapsBcp47TagsCorrectly(string storedValue, string? expectedOverride)
    {
        Assert.Equal(expectedOverride, LanguagePreference.ResolveOverride(storedValue));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("fr-FR")]
    [InlineData("zh")]
    [InlineData("garbage")]
    public void InvalidLanguageSettingFallsBackToSystem(string? storedValue)
    {
        Assert.Equal(LanguagePreference.SystemValue, LanguagePreference.Normalize(storedValue));
        Assert.Null(LanguagePreference.ResolveOverride(storedValue));
    }

    [Fact]
    public void FavoriteStableIdsAreUnaffectedByLanguage()
    {
        foreach (var language in ReswCatalog.Languages)
        {
            var folder = Path.Combine(Path.GetTempPath(), $"UrbanPlanToolbox-{Guid.NewGuid():N}");
            var path = Path.Combine(folder, "settings.json");
            try
            {
                var service = new FavoriteToolsService(new SettingsService(path), ToolRegistry.Default);
                service.Add(ToolIds.PlanningIndicatorCalculator);
                service.Add(ToolIds.UnitScaleConverter);

                var stored = new SettingsService(path).Load();
                Assert.Equal(
                    [ToolIds.PlanningIndicatorCalculator, ToolIds.UnitScaleConverter],
                    stored.FavoriteToolIds);

                var localization = TestLocalization.For(language);
                foreach (var id in stored.FavoriteToolIds)
                {
                    var tool = ToolRegistry.Default.GetById(id);
                    Assert.False(string.IsNullOrWhiteSpace(localization.GetString(tool.NameResourceKey)));
                }
            }
            finally
            {
                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, recursive: true);
                }
            }
        }
    }

    [Theory]
    [InlineData("zh-CN", "规划指标快速计算器", "单位与比例尺换算器")]
    [InlineData("ja-JP", "計画指標計算", "単位・縮尺変換")]
    [InlineData("en-US", "Planning Metrics Calculator", "Unit & Scale Converter")]
    public void BothToolsResolveNamesAndDescriptionsInAllThreeLanguages(
        string language,
        string expectedCalculatorName,
        string expectedConverterName)
    {
        var localization = TestLocalization.For(language);
        var calculator = ToolRegistry.Default.GetById(ToolIds.PlanningIndicatorCalculator);
        var converter = ToolRegistry.Default.GetById(ToolIds.UnitScaleConverter);

        Assert.Equal(expectedCalculatorName, localization.GetString(calculator.NameResourceKey));
        Assert.Equal(expectedConverterName, localization.GetString(converter.NameResourceKey));
        Assert.False(string.IsNullOrWhiteSpace(localization.GetString(calculator.DescriptionResourceKey)));
        Assert.False(string.IsNullOrWhiteSpace(localization.GetString(converter.DescriptionResourceKey)));
    }

    [Fact]
    public void SearchIndexUsesCurrentLanguageText()
    {
        var zh = new ToolSearchService(ToolRegistry.Default, TestLocalization.ZhCn);
        var ja = new ToolSearchService(ToolRegistry.Default, TestLocalization.JaJp);
        var en = new ToolSearchService(ToolRegistry.Default, TestLocalization.EnUs);

        Assert.Contains(ToolIds.PlanningIndicatorCalculator, Flatten(zh.Search("规划", _ => false)).Select(tool => tool.Id));
        Assert.Equal(ToolIds.PlanningIndicatorCalculator, Assert.Single(Flatten(ja.Search("容積率", _ => false))).Id);
        Assert.Contains(ToolIds.PlanningIndicatorCalculator, Flatten(en.Search("planning", _ => false)).Select(tool => tool.Id));
        Assert.Equal(ToolIds.UnitScaleConverter, Assert.Single(Flatten(ja.Search("縮尺", _ => false))).Id);
        Assert.Equal(ToolIds.UnitScaleConverter, Assert.Single(Flatten(en.Search("scale", _ => false))).Id);

        // The index is language-specific rather than a fixed Chinese index
        // (the stable ID still matches in every language by design).
        Assert.Empty(Flatten(zh.Search("metrics", _ => false)));
        Assert.Empty(Flatten(en.Search("规划", _ => false)));
    }

    [Fact]
    public void SearchResultsReturnSameStableToolIdsAcrossLanguages()
    {
        foreach (var language in ReswCatalog.Languages)
        {
            var service = new ToolSearchService(ToolRegistry.Default, TestLocalization.For(language));
            var ids = Flatten(service.Search(string.Empty, _ => false)).Select(tool => tool.Id).ToArray();
            Assert.Equal([ToolIds.UnitScaleConverter, ToolIds.DesignConceptDictionary, ToolIds.PlanningIndicatorCalculator, ToolIds.WorkflowReviewChecklist, ToolIds.ColorPaletteRecorder, ToolIds.RegulationsIndex], ids);
        }
    }

    [Fact]
    public void UnknownResourceKeyReturnsPlaceholderWithoutCrash()
    {
        var localization = new DictionaryLocalizationService(new Dictionary<string, string>(StringComparer.Ordinal));

        Assert.Equal("!Missing_Key!", localization.GetString("Missing_Key"));

        var search = new ToolSearchService(ToolRegistry.Default, localization);
        var groups = search.Search(string.Empty, _ => false);
        Assert.NotEmpty(groups);
        Assert.All(
            Flatten(groups),
            tool =>
            {
                Assert.StartsWith("!", tool.DisplayName);
                Assert.EndsWith("!", tool.DisplayName);
            });
    }

    [Fact]
    public void VersionConfigurationIs040()
    {
        var manifest = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Package.appxmanifest"));
        Assert.Contains("Version=\"0.4.3.0\"", manifest);
        var languages = Regex.Matches(manifest, "<Resource Language=\\\"([^\\\"]+)\\\"")
            .Select(match => match.Groups[1].Value).ToArray();
        Assert.Equal(["zh-CN", "ja-JP", "en-US"], languages);
        Assert.Contains("ms-resource:AppDisplayName", manifest);

        var project = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "UrbanPlanToolbox.csproj"));
        Assert.Contains("<Version>0.4.3</Version>", project);
        Assert.Contains("<AssemblyVersion>0.4.3</AssemblyVersion>", project);
        Assert.Contains("<FileVersion>0.4.3</FileVersion>", project);
        Assert.Contains("<DefaultLanguage>zh-CN</DefaultLanguage>", project);
        Assert.Contains("<AppxBundleAutoResourcePackageQualifiers>Scale|DXFeatureLevel</AppxBundleAutoResourcePackageQualifiers>", project);
        Assert.DoesNotContain("<AppxBundleAutoResourcePackageQualifiers>Language", project);
    }

    [Fact]
    public void AllCodeReferencedResourceKeysExist()
    {
        var catalogs = ReswCatalog.Languages.ToDictionary(language => language, ReswCatalog.Load);
        var sourceRoot = FindRepositoryRoot();

        var codeKeys = ExtractKeys(
            new[]
            {
                Path.Combine(sourceRoot, "App.xaml.cs"),
                Path.Combine(sourceRoot, "MainPage.xaml.cs"),
                Path.Combine(sourceRoot, "Views"),
                Path.Combine(sourceRoot, "Controls"),
                Path.Combine(sourceRoot, "Services"),
                Path.Combine(sourceRoot, "Helpers")
            }
                .Where(path => Directory.Exists(path) || File.Exists(path))
                .SelectMany(path => Directory.Exists(path)
                    ? Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories)
                    : [path]),
            ResourceKeyPattern);
        Assert.NotEmpty(codeKeys);

        foreach (var language in ReswCatalog.Languages)
        {
            foreach (var key in codeKeys)
            {
                Assert.True(catalogs[language].ContainsKey(key), $"Missing resource key '{key}' in {language}.");
            }
        }

        var uids = ExtractKeys(
            new[]
            {
                Path.Combine(sourceRoot, "MainPage.xaml"),
                Path.Combine(sourceRoot, "Views"),
                Path.Combine(sourceRoot, "Controls")
            }
                .Where(path => Directory.Exists(path) || File.Exists(path))
                .SelectMany(path => Directory.Exists(path)
                    ? Directory.GetFiles(path, "*.xaml", SearchOption.AllDirectories)
                    : [path]),
            XamlUidPattern);
        Assert.NotEmpty(uids);

        foreach (var uid in uids)
        {
            foreach (var language in ReswCatalog.Languages)
            {
                Assert.True(
                    catalogs[language].Keys.Any(key => key.StartsWith(uid + ".", StringComparison.Ordinal)),
                    $"Missing x:Uid property resources for '{uid}' in {language}.");
            }
        }
    }

    [Fact]
    public void ButtonUidResourcesDoNotSetUnsupportedHeaderProperty()
    {
        var sourceRoot = FindRepositoryRoot();
        var buttonUids = ExtractKeys(
            new[]
            {
                Path.Combine(sourceRoot, "MainPage.xaml"),
                Path.Combine(sourceRoot, "Views"),
                Path.Combine(sourceRoot, "Controls")
            }
                .Where(path => Directory.Exists(path) || File.Exists(path))
                .SelectMany(path => Directory.Exists(path)
                    ? Directory.GetFiles(path, "*.xaml", SearchOption.AllDirectories)
                    : [path]),
            XamlButtonUidPattern);

        Assert.NotEmpty(buttonUids);
        foreach (var language in ReswCatalog.Languages)
        {
            var resources = ReswCatalog.Load(language);
            foreach (var uid in buttonUids)
            {
                Assert.False(
                    resources.ContainsKey($"{uid}.Header"),
                    $"Button x:Uid '{uid}' sets unsupported Header property in {language}; use Content instead.");
            }
        }
    }

    [Fact]
    public void XamlUidResourcesOnlyTargetPropertiesSupportedByTheirElementType()
    {
        var allowed = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
        {
            ["Button"] = ["Content", "AutomationProperties.Name", "ToolTipService.ToolTip"],
            ["CheckBox"] = ["Content"],
            ["ComboBox"] = ["Header"],
            ["ComboBoxItem"] = ["Content"],
            ["InfoBar"] = ["Message", "Title"],
            ["PivotItem"] = ["Header"],
            ["TextBlock"] = ["Text"],
            ["TextBox"] = ["PlaceholderText"],
            ["ToggleSwitch"] = ["Header"]
        };
        var sourceRoot = FindRepositoryRoot();
        var xamlFiles = new[] { Path.Combine(sourceRoot, "MainPage.xaml"), Path.Combine(sourceRoot, "Views"), Path.Combine(sourceRoot, "Controls") }
            .Where(path => Directory.Exists(path) || File.Exists(path))
            .SelectMany(path => Directory.Exists(path) ? Directory.GetFiles(path, "*.xaml", SearchOption.AllDirectories) : [path]);
        var uidTargets = xamlFiles.SelectMany(path => XDocument.Load(path).Descendants())
            .Select(element => new { Type = element.Name.LocalName, Uid = element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "Uid")?.Value })
            .Where(item => item.Uid is not null)
            .ToArray();

        foreach (var language in ReswCatalog.Languages)
        {
            var resources = ReswCatalog.Load(language);
            foreach (var target in uidTargets)
            {
                Assert.True(allowed.TryGetValue(target.Type, out var properties), $"Add an explicit property allow-list for XAML element '{target.Type}'.");
                foreach (var key in resources.Keys.Where(key => key.StartsWith(target.Uid + ".", StringComparison.Ordinal)))
                {
                    var property = key[(target.Uid!.Length + 1)..];
                    Assert.Contains(property, properties!);
                }
            }
        }
    }

    [Fact]
    public void CodeResourceLookupsDoNotUseXamlPropertyKeys()
    {
        var sourceRoot = FindRepositoryRoot();
        var files = new[]
        {
            Path.Combine(sourceRoot, "App.xaml.cs"),
            Path.Combine(sourceRoot, "MainPage.xaml.cs"),
            Path.Combine(sourceRoot, "Views"),
            Path.Combine(sourceRoot, "Controls"),
            Path.Combine(sourceRoot, "Services"),
            Path.Combine(sourceRoot, "Helpers")
        }
            .Where(path => Directory.Exists(path) || File.Exists(path))
            .SelectMany(path => Directory.Exists(path) ? Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories) : [path]);

        var propertyKeys = ExtractKeys(files, CodeResourceKeyPattern)
            .Where(key => key.Contains('.', StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(propertyKeys);
        Assert.Equal("WGS 84 Coordinates (Optional)", TestLocalization.EnUs.GetString("Project_Coordinates_Wgs84_Label"));
        Assert.Equal("WGS 84 坐标（可选）", TestLocalization.ZhCn.GetString("Project_Coordinates_Wgs84_Label"));
        Assert.Equal("WGS 84 座標（任意）", TestLocalization.JaJp.GetString("Project_Coordinates_Wgs84_Label"));
    }

    private static IEnumerable<LocalizedTool> Flatten(IReadOnlyList<ToolSearchGroup> groups) =>
        groups.SelectMany(group => group.Tools);

    private static HashSet<string> ExtractKeys(IEnumerable<string> files, Func<Regex> patternFactory)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            foreach (Match match in patternFactory().Matches(text))
            {
                keys.Add(match.Groups[1].Value);
            }
        }

        return keys;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "UrbanPlanToolbox.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root from test output.");
    }

    private static HashSet<int> ExtractPlaceholders(string value) =>
        [.. PlaceholderPattern().Matches(value)
            .Select(match => int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture))];

    [GeneratedRegex(@"\{(\d+)\}", RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderPattern();

    [GeneratedRegex(@"(?:GetString|GetFormattedString)\(""([A-Za-z0-9_]+)""", RegexOptions.CultureInvariant)]
    private static partial Regex ResourceKeyPattern();

    [GeneratedRegex(@"(?:GetString|GetFormattedString)\(""([A-Za-z0-9_.]+)""", RegexOptions.CultureInvariant)]
    private static partial Regex CodeResourceKeyPattern();

    [GeneratedRegex(@"x:Uid=""([A-Za-z0-9_]+)""", RegexOptions.CultureInvariant)]
    private static partial Regex XamlUidPattern();

    [GeneratedRegex(@"<Button\b[^>]*\bx:Uid=""([A-Za-z0-9_]+)""", RegexOptions.CultureInvariant)]
    private static partial Regex XamlButtonUidPattern();
}
