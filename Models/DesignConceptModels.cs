namespace UrbanPlanToolbox.Models;

public sealed class DesignConceptDictionaryDocument
{
    public List<DesignConcept> Concepts { get; set; } = [];
}

public sealed class DesignConcept
{
    public Guid ConceptId { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Definition { get; set; } = string.Empty;
    public List<string> ApplicableProjectTypes { get; set; } = [];
    public List<string> Tags { get; set; } = [];
    public string? SourceOrReference { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class DesignConceptDraft
{
    public string Name { get; set; } = string.Empty;
    public string Definition { get; set; } = string.Empty;
    public List<string> ApplicableProjectTypes { get; set; } = [];
    public List<string> Tags { get; set; } = [];
    public string? SourceOrReference { get; set; }
    public string? Notes { get; set; }
}

public enum DesignConceptSort
{
    LastModified,
    Created,
    Name
}

public sealed record DesignConceptEditSnapshot(
    string Name,
    string Definition,
    IReadOnlyList<string> ApplicableProjectTypes,
    IReadOnlyList<string> Tags,
    string SourceOrReference,
    string Notes);
