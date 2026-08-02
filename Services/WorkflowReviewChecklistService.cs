using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Models.Tools;

namespace UrbanPlanToolbox.Services;

public sealed class WorkflowReviewChecklistService
{
    public const int WorkflowReviewChecklistSchemaVersion = 1;
    public const string DataFileName = "checklists.json";
    private readonly JsonDataStorage _storage;

    public WorkflowReviewChecklistService(IAppDataPathProvider paths, IStorageDiagnostics? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _storage = new JsonDataStorage(paths, WorkflowReviewChecklistSchemaVersion, diagnostics: diagnostics);
    }

    public async Task<DataReadResult<List<WorkflowReviewChecklistDocument>>> ReadAsync(CancellationToken cancellationToken = default)
    {
        var result = await _storage.ReadAsync<List<WorkflowReviewChecklistDocument>>(ToolIds.WorkflowReviewChecklist, DataFileName, cancellationToken);
        if (result.Status == DataStorageStatus.NotFound) return new(DataStorageStatus.Success, [], WorkflowReviewChecklistSchemaVersion);
        if (result.HasValue && !TryValidateDocuments(result.Value!, out _)) return new(DataStorageStatus.Corrupt, null, result.SchemaVersion, "ChecklistInvalid");
        return result;
    }

    public Task<DataWriteResult> SaveAsync(IReadOnlyList<WorkflowReviewChecklistDocument> documents, CancellationToken cancellationToken = default)
    {
        var copy = documents.Select(Clone).ToList();
        return !TryValidateDocuments(copy, out var error)
            ? Task.FromResult(new DataWriteResult(DataStorageStatus.IoFailure, error))
            : _storage.SaveAsync(ToolIds.WorkflowReviewChecklist, DataFileName, copy, cancellationToken);
    }

    public static WorkflowReviewChecklistDocument Clone(WorkflowReviewChecklistDocument source) => new()
    {
        ChecklistId = source.ChecklistId, Name = source.Name, Description = source.Description, UsageType = source.UsageType,
        CreatedAt = source.CreatedAt, UpdatedAt = source.UpdatedAt,
        Sections = source.Sections.Select(section => new WorkflowChecklistSection
        {
            SectionId = section.SectionId, Title = section.Title, Description = section.Description, SortOrder = section.SortOrder,
            Items = section.Items.Select(item => new WorkflowChecklistItem
            {
                ItemId = item.ItemId, Title = item.Title, Description = item.Description, Status = item.Status,
                IsCritical = item.IsCritical, Note = item.Note, SortOrder = item.SortOrder
            }).ToList()
        }).ToList()
    };

    public static WorkflowChecklistStatistics GetStatistics(WorkflowReviewChecklistDocument document)
    {
        var items = document.Sections.SelectMany(section => section.Items).ToArray();
        var pending = items.Count(item => item.Status == WorkflowChecklistItemStatus.Pending);
        var passed = items.Count(item => item.Status == WorkflowChecklistItemStatus.Passed);
        var needsRevision = items.Count(item => item.Status == WorkflowChecklistItemStatus.NeedsRevision);
        var notApplicable = items.Count(item => item.Status == WorkflowChecklistItemStatus.NotApplicable);
        var completed = passed + needsRevision;
        var total = items.Length - notApplicable;
        return new(items.Length, pending, passed, needsRevision, notApplicable, completed, total == 0 ? 0 : completed * 100d / total);
    }

    public static bool TryValidateDocuments(IEnumerable<WorkflowReviewChecklistDocument> documents, out string? error)
    {
        error = null;
        var checklistIds = new HashSet<Guid>();
        foreach (var checklist in documents)
        {
            if (checklist.ChecklistId == Guid.Empty || !checklistIds.Add(checklist.ChecklistId) || string.IsNullOrWhiteSpace(checklist.Name)) { error = "ChecklistInvalid"; return false; }
            if (checklist.CreatedAt.Offset != TimeSpan.Zero || checklist.UpdatedAt.Offset != TimeSpan.Zero || checklist.UpdatedAt < checklist.CreatedAt) { error = "ChecklistTimestampInvalid"; return false; }
            var sectionIds = new HashSet<Guid>();
            foreach (var section in checklist.Sections)
            {
                if (section.SectionId == Guid.Empty || !sectionIds.Add(section.SectionId) || string.IsNullOrWhiteSpace(section.Title)) { error = "SectionInvalid"; return false; }
                var itemIds = new HashSet<Guid>();
                foreach (var item in section.Items)
                {
                    if (item.ItemId == Guid.Empty || !itemIds.Add(item.ItemId) || string.IsNullOrWhiteSpace(item.Title) || !Enum.IsDefined(item.Status)) { error = "ChecklistItemInvalid"; return false; }
                }
            }
        }
        return true;
    }

    public static void NormalizeSortOrders(WorkflowReviewChecklistDocument document)
    {
        for (var sectionIndex = 0; sectionIndex < document.Sections.Count; sectionIndex++)
        {
            document.Sections[sectionIndex].SortOrder = sectionIndex;
            var items = document.Sections[sectionIndex].Items;
            for (var itemIndex = 0; itemIndex < items.Count; itemIndex++) items[itemIndex].SortOrder = itemIndex;
        }
    }
}
