namespace UrbanPlanToolbox.Models;

public enum WorkflowChecklistUsageType { Design, Research, General }
public enum WorkflowChecklistItemStatus { Pending, Passed, NeedsRevision, NotApplicable }

public sealed class WorkflowReviewChecklistDocument
{
    public Guid ChecklistId { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public WorkflowChecklistUsageType UsageType { get; set; } = WorkflowChecklistUsageType.General;
    public List<WorkflowChecklistSection> Sections { get; init; } = [];
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class WorkflowChecklistSection
{
    public Guid SectionId { get; init; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public List<WorkflowChecklistItem> Items { get; init; } = [];
}

public sealed class WorkflowChecklistItem
{
    public Guid ItemId { get; init; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public WorkflowChecklistItemStatus Status { get; set; } = WorkflowChecklistItemStatus.Pending;
    public bool IsCritical { get; set; }
    public string? Note { get; set; }
    public int SortOrder { get; set; }
}

public sealed record WorkflowChecklistStatistics(int Total, int Pending, int Passed, int NeedsRevision, int NotApplicable, int Completed, double CompletionRate);
