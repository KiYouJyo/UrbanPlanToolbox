using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Models.Tools;
using UrbanPlanToolbox.Services;
using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class DesignConceptDictionaryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"UrbanPlanToolbox-concepts-{Guid.NewGuid():N}");
    private readonly AppDataPathProvider _paths;
    private readonly DesignConceptDictionaryService _service;

    public DesignConceptDictionaryTests()
    {
        _paths = new AppDataPathProvider(_root, [ToolIds.DesignConceptDictionary]);
        _service = new DesignConceptDictionaryService(_paths);
    }

    [Fact]
    public async Task EmptyDictionaryIsOfflineAndRoundTripsWithSchemaOne()
    {
        var empty = await _service.ReadAsync();
        Assert.True(empty.HasValue);
        Assert.Empty(empty.Value!.Concepts);

        var created = CreateConcept("三生空间", "在生态、生活与生产之间建立关系。", ["综合体", "综合体"], ["生态", "生态"], "现场研究");
        Assert.True((await _service.SaveAsync(new DesignConceptDictionaryDocument { Concepts = [created] })).Succeeded);

        var loaded = await new DesignConceptDictionaryService(_paths).ReadAsync();
        var restored = Assert.Single(loaded.Value!.Concepts);
        Assert.Equal(created.ConceptId, restored.ConceptId);
        Assert.Equal(["综合体"], restored.ApplicableProjectTypes);
        Assert.Equal(["生态"], restored.Tags);
        Assert.Contains("\"schemaVersion\": 1", await File.ReadAllTextAsync(_paths.GetToolDataFilePath(ToolIds.DesignConceptDictionary, DesignConceptDictionaryService.DataFileName)));
    }

    [Fact]
    public void DraftValidationTrimsAndDeduplicatesWithoutAcceptingBlankRequiredFields()
    {
        var now = DateTimeOffset.UtcNow;
        var draft = new DesignConceptDraft
        {
            Name = "  三生空间 ",
            Definition = "  关系优先  ",
            ApplicableProjectTypes = ["  公共建筑", "公共建筑", ""],
            Tags = ["生态", " 生态 "],
            SourceOrReference = "  reference ",
            Notes = " notes "
        };

        Assert.True(DesignConceptDictionaryService.TryBuildConcept(draft, Guid.NewGuid(), now, now, out var concept, out _));
        Assert.Equal("三生空间", concept.Name);
        Assert.Equal(["公共建筑"], concept.ApplicableProjectTypes);
        Assert.Equal(["生态"], concept.Tags);
        Assert.Equal("reference", concept.SourceOrReference);

        draft.Name = " ";
        Assert.False(DesignConceptDictionaryService.TryBuildConcept(draft, Guid.NewGuid(), now, now, out _, out var error));
        Assert.Equal("ConceptNameRequired", error);
    }

    [Fact]
    public void SearchFiltersEveryBusinessFieldAndSortsDeterministically()
    {
        var first = CreateConcept("三生空间", "生态与生活", ["公共建筑"], ["生态"], "reference-a");
        first.Notes = "关系优先";
        var second = CreateConcept("渐进式更新", "存量改造", ["城市更新"], ["韧性"], "reference-b");
        var document = new DesignConceptDictionaryDocument { Concepts = [first, second] };

        Assert.Equal(first, Assert.Single(DesignConceptDictionaryService.Search(document, "关系优先", null, null, DesignConceptSort.Name)));
        Assert.Equal(first, Assert.Single(DesignConceptDictionaryService.Search(document, null, "公共建筑", "生态", DesignConceptSort.Name)));
        Assert.Equal(["公共建筑", "城市更新"], DesignConceptDictionaryService.GetProjectTypes(document));
        Assert.Equal(["生态", "韧性"], DesignConceptDictionaryService.GetTags(document));
    }

    [Fact]
    public void CopiesAreDeepAndUseNewStableIds()
    {
        var source = CreateConcept("原理念", "定义", ["住宅"], ["共享"], null);
        var copy = DesignConceptDictionaryService.CreateCopy(source, "副本", DateTimeOffset.UtcNow.AddMinutes(1));

        Assert.NotEqual(source.ConceptId, copy.ConceptId);
        Assert.Equal("原理念 副本", copy.Name);
        copy.ApplicableProjectTypes.Add("公共建筑");
        copy.Tags.Clear();
        Assert.Equal(["住宅"], source.ApplicableProjectTypes);
        Assert.Equal(["共享"], source.Tags);
    }

    [Fact]
    public void ConceptPageKeepsVirtualizationAndUsesOneWholeCardAction()
    {
        var root = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "Views", "DesignConceptDictionaryPage.xaml"));
        Assert.Contains("<ListView x:Name=\"ConceptsList\"", xaml);
        Assert.Contains("<ListView.ItemTemplate>", xaml);
        Assert.Contains("Click=\"OnConceptClick\"", xaml);
        Assert.Contains("TextWrapping=\"Wrap\"", xaml);
        Assert.DoesNotContain("EditButton", xaml);
        Assert.DoesNotContain("DeleteButton", xaml[..xaml.IndexOf("<ListView", StringComparison.Ordinal)]);
        var tagXaml = File.ReadAllText(Path.Combine(root, "Controls", "EditableTagList.xaml"));
        Assert.Contains("ItemsWrapGrid", tagXaml);
        Assert.Contains("AutomationProperties.Name=\"Remove tag\"", tagXaml);
    }

    [Fact]
    public void ConceptPageUsesDisplayMembersAndStableResponsiveContainers()
    {
        var root = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "Views", "DesignConceptDictionaryPage.xaml"));
        Assert.Equal(3, Count(xaml, "DisplayMemberPath=\"Display\""));
        Assert.Equal(3, Count(xaml, "SelectedValuePath=\"Value\""));
        Assert.Contains("HorizontalContentAlignment=\"Stretch\"", xaml);
        Assert.Contains("AdaptiveTrigger MinWindowWidth=\"720\"", xaml);
        Assert.DoesNotContain("ActualWidth", xaml);
        Assert.DoesNotContain("FilterChoice.ToString", xaml);
        Assert.DoesNotContain("Width=\"400\"", xaml);
    }

    [Fact]
    public void SnapshotNormalizesTextAndReportsOnlyBusinessFieldDifferences()
    {
        var concept = CreateConcept("名称", "第一行\r\n第二行", ["住宅"], ["生态"], null);
        concept.Notes = null;
        var baseline = DesignConceptDictionaryService.CreateEditSnapshot(concept);
        var same = DesignConceptDictionaryService.CreateEditSnapshot(new DesignConcept
        {
            Name = " 名称 ", Definition = "第一行\n第二行", ApplicableProjectTypes = ["住宅"], Tags = ["生态"], Notes = string.Empty,
            CreatedAt = concept.CreatedAt, UpdatedAt = concept.UpdatedAt
        });
        Assert.False(DesignConceptDictionaryService.HasBusinessChanges(baseline, same));

        same = same with { Notes = "changed" };
        Assert.Equal([nameof(DesignConcept.Notes)], DesignConceptDictionaryService.GetChangedFields(baseline, same));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private static DesignConcept CreateConcept(string name, string definition, IEnumerable<string> projectTypes, IEnumerable<string> tags, string? source) => new()
    {
        Name = name,
        Definition = definition,
        ApplicableProjectTypes = [.. projectTypes],
        Tags = [.. tags],
        SourceOrReference = source,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && (!File.Exists(Path.Combine(directory.FullName, "UrbanPlanToolbox.csproj")) || !Directory.Exists(Path.Combine(directory.FullName, "Views")))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }

    private static int Count(string text, string value) => text.Split(value, StringSplitOptions.None).Length - 1;
}
