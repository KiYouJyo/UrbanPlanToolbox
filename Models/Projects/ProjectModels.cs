namespace UrbanPlanToolbox.Models.Projects;

public static class ProjectTypeCodes
{
    public const string Coursework = "coursework";
    public const string Competition = "competition";
    public const string Research = "research";
    public const string Professional = "professional";
    public const string Personal = "personal";
    public const string Other = "other";

    public static IReadOnlyList<string> All { get; } =
        [Coursework, Competition, Research, Professional, Personal, Other];

    public static bool IsValid(string? value) => value is not null && All.Contains(value, StringComparer.Ordinal);
}

public sealed class ProjectRecord
{
    public required Guid Id { get; init; }
    public required string Name { get; set; }
    public required string Type { get; set; }
    public string? CustomType { get; set; }
    public string? AdministrativeArea { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? Description { get; set; }
    public List<ProjectTodoItem> Todos { get; init; } = [];
    public List<PlanningSnapshot> PlanningSnapshots { get; init; } = [];
    public ProjectFolderReference? WorkFolder { get; set; }
    public bool IsArchived { get; set; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public required DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset? ArchivedAtUtc { get; set; }
}

public sealed class ProjectTodoItem
{
    public required Guid Id { get; init; }
    public required string Title { get; set; }
    public bool IsCompleted { get; set; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public int DisplayOrder { get; set; }
}

public sealed class PlanningSnapshot
{
    public required Guid Id { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public string? Name { get; set; }
    public required PlanningInput Input { get; init; }
    public required PlanningResult Result { get; init; }
    public string CalculationModel { get; init; } = "planning-indicator-v1";
}

public sealed class ProjectFolderReference
{
    public string? AccessToken { get; set; }
    public required string DisplayName { get; set; }
    public required string DisplayPath { get; set; }
    public bool RequiresReselection { get; set; }
}

public sealed class ProjectIndex
{
    public List<ProjectIndexEntry> Projects { get; init; } = [];
}

public sealed class ProjectIndexEntry
{
    public required Guid Id { get; init; }
    public required string Name { get; set; }
    public required string Type { get; set; }
    public bool IsArchived { get; set; }
    public required DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset? ArchivedAtUtc { get; set; }
}

public sealed record ProjectIssue(Guid ProjectId, DataStorageStatus Status, string? FailureType = null);

public sealed record ProjectListResult(
    IReadOnlyList<ProjectRecord> Projects,
    IReadOnlyList<ProjectIssue> Issues);

public sealed record ProjectSaveResult(
    DataStorageStatus Status,
    ProjectRecord? Project = null,
    IReadOnlyList<string>? ValidationErrors = null,
    string? FailureType = null)
{
    public bool Succeeded => Status == DataStorageStatus.Success && ValidationErrors is null;
}
