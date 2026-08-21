using UrbanPlanToolbox.Services;
using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class DesignConceptLocalizationTests
{
    [Fact]
    public void ParsesAndResolvesCategoryProjectTypeAndTagLabels()
    {
        const string json = """
        {
          "labels": {
            "categories": { "城市结构": { "zh-CN": "城市结构", "ja-JP": "都市構造", "en-US": "Urban Structure" } },
            "projectTypes": { "城市设计": { "zh-CN": "城市设计", "ja-JP": "都市デザイン", "en-US": "Urban Design" } },
            "tags": { "场所认知": { "zh-CN": "场所认知", "ja-JP": "場所認知", "en-US": "Place Identity" } }
          }
        }
        """;

        var labels = DesignConceptLocalization.Parse(json);
        Assert.Equal("都市構造", labels.Category("城市结构", "ja-JP"));
        Assert.Equal("Urban Design", labels.ProjectType("城市设计", "en-US"));
        Assert.Equal("Place Identity", labels.Tag("场所认知", "en-US"));
        Assert.Equal("城市结构", labels.Category("城市结构", "zh-CN"));
    }

    [Fact]
    public void NonChineseUiNeverFallsBackToCanonicalChineseMetadata()
    {
        const string json = """
        {
          "labels": {
            "categories": { "城市结构": { "zh-CN": "城市结构", "ja-JP": "都市構造", "en-US": "Urban Structure" } },
            "projectTypes": {},
            "tags": {}
          }
        }
        """;

        var labels = DesignConceptLocalization.Parse(json);
        Assert.Equal(string.Empty, labels.ProjectType("历史环境", "ja-JP"));
        Assert.Equal(string.Empty, labels.Tag("公共空间", "en-US"));
        Assert.Equal("历史环境", labels.ProjectType("历史环境", "zh-CN"));
    }

    [Fact]
    public void SearchTermsIncludeAllLocalizedMetadataWithoutChangingCanonicalKeys()
    {
        const string json = """
        {
          "labels": {
            "categories": { "城市结构": { "zh-CN": "城市结构", "ja-JP": "都市構造", "en-US": "Urban Structure" } },
            "projectTypes": { "城市设计": { "zh-CN": "城市设计", "ja-JP": "都市デザイン", "en-US": "Urban Design" } },
            "tags": { "场所认知": { "zh-CN": "场所认知", "ja-JP": "場所認知", "en-US": "Place Identity" } }
          }
        }
        """;

        var labels = DesignConceptLocalization.Parse(json);
        var terms = labels.SearchTerms("城市结构", ["城市设计"], ["场所认知"]).ToArray();
        Assert.Contains("城市结构", terms);
        Assert.Contains("都市構造", terms);
        Assert.Contains("Urban Structure", terms);
        Assert.Contains("都市デザイン", terms);
        Assert.Contains("Place Identity", terms);
    }
}
