using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Models.Tools;
using UrbanPlanToolbox.Services;
using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class WorkflowReviewChecklistTests
{
    [Fact]
    public async Task ChecklistRoundTripsWithStableIdsAndIndependentCopies()
    {
        using var scope = new Scope();
        var service = new WorkflowReviewChecklistService(scope.Provider);
        var checklist = new WorkflowReviewChecklistDocument { Name = "Review" };
        var section = new WorkflowChecklistSection { Title = "Analysis" };
        section.Items.Add(new WorkflowChecklistItem { Title = "Check source", Status = WorkflowChecklistItemStatus.Passed, IsCritical = true });
        checklist.Sections.Add(section);

        Assert.True((await service.SaveAsync([checklist])).Succeeded);
        var loaded = await service.ReadAsync();
        var value = Assert.Single(loaded.Value!);
        Assert.Equal(checklist.ChecklistId, value.ChecklistId);
        Assert.Equal(section.SectionId, value.Sections[0].SectionId);
        Assert.Equal(section.Items[0].ItemId, value.Sections[0].Items[0].ItemId);
        value.Sections[0].Items[0].Note = "draft";
        Assert.Null(checklist.Sections[0].Items[0].Note);
    }

    [Fact]
    public void StatisticsExcludeNotApplicableFromCompletionDenominator()
    {
        var checklist = new WorkflowReviewChecklistDocument { Name = "Review" };
        var section = new WorkflowChecklistSection { Title = "Stage" };
        section.Items.Add(new WorkflowChecklistItem { Title = "Pass", Status = WorkflowChecklistItemStatus.Passed });
        section.Items.Add(new WorkflowChecklistItem { Title = "Skip", Status = WorkflowChecklistItemStatus.NotApplicable });
        section.Items.Add(new WorkflowChecklistItem { Title = "Pending", Status = WorkflowChecklistItemStatus.Pending });
        checklist.Sections.Add(section);
        var stats = WorkflowReviewChecklistService.GetStatistics(checklist);
        Assert.Equal(3, stats.Total);
        Assert.Equal(1, stats.NotApplicable);
        Assert.Equal(50, stats.CompletionRate);
    }

    [Fact]
    public async Task FutureSchemaIsRefusedAndExistingDataRemains()
    {
        using var scope = new Scope();
        var service = new WorkflowReviewChecklistService(scope.Provider);
        var path = scope.Provider.GetToolDataFilePath(ToolIds.WorkflowReviewChecklist, WorkflowReviewChecklistService.DataFileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, "{\"schemaVersion\":2,\"savedAtUtc\":\"2026-01-01T00:00:00Z\",\"payload\":[]}");
        var result = await service.ReadAsync();
        Assert.Equal(DataStorageStatus.UnsupportedFutureVersion, result.Status);
    }

    private sealed class Scope : IDisposable
    {
        public Scope() { Root = Path.Combine(Path.GetTempPath(), $"UrbanPlanToolbox-workflow-{Guid.NewGuid():N}"); Provider = new AppDataPathProvider(Root, [ToolIds.WorkflowReviewChecklist]); }
        public string Root { get; }
        public AppDataPathProvider Provider { get; }
        public void Dispose() { if (Directory.Exists(Root)) Directory.Delete(Root, true); }
    }
}
