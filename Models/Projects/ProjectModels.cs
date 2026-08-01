using System.Text.Json.Serialization;

namespace UrbanPlanToolbox.Models.Projects;

public static class ProjectKindCodes
{
    public const string Design = "design";
    public const string Research = "research";

    public static IReadOnlyList<string> All { get; } = [Design, Research];
    public static bool IsValid(string? value) => value is not null && All.Contains(value, StringComparer.Ordinal);
}

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

public static class ResearchProjectTypeCodes
{
    public const string Coursework = "coursework";
    public const string Thesis = "thesis";
    public const string Paper = "paper";
    public const string ResearchProject = "research-project";
    public const string Other = "other";

    public static IReadOnlyList<string> All { get; } = [Coursework, Thesis, Paper, ResearchProject, Other];
    public static bool IsValid(string? value) => value is not null && All.Contains(value, StringComparer.Ordinal);
}

public sealed class DesignProjectDetails
{
    public string? AdministrativeRegion { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? Description { get; set; }
    public string? PlanningRequirements { get; set; }
}

public sealed class ResearchProjectDetails
{
    public string? ResearchField { get; set; }
    public string? ResearchSubject { get; set; }
    public string? ResearchMethods { get; set; }
}

public sealed class ProjectRecord
{
    public required Guid Id { get; init; }
    public string Kind { get; init; } = ProjectKindCodes.Design;
    public required string Name { get; set; }
    public required string Type { get; set; }
    public string? CustomType { get; set; }
    public DesignProjectDetails? DesignDetails { get; set; }
    public ResearchProjectDetails? ResearchDetails { get; set; }

    // Source compatibility for the existing design workspace. Schema v3 persists
    // these values only inside DesignDetails.
    [JsonIgnore] public string? AdministrativeArea { get => DesignDetails?.AdministrativeRegion; set { EnsureDesignDetails(); DesignDetails!.AdministrativeRegion = value; } }
    [JsonIgnore] public decimal? Latitude { get => DesignDetails?.Latitude; set { EnsureDesignDetails(); DesignDetails!.Latitude = value; } }
    [JsonIgnore] public decimal? Longitude { get => DesignDetails?.Longitude; set { EnsureDesignDetails(); DesignDetails!.Longitude = value; } }
    [JsonIgnore] public string? Description { get => DesignDetails?.Description; set { EnsureDesignDetails(); DesignDetails!.Description = value; } }
    [JsonIgnore] public string? PlanningRequirements { get => DesignDetails?.PlanningRequirements; set { EnsureDesignDetails(); DesignDetails!.PlanningRequirements = value; } }
    public List<ProjectMilestone> Milestones { get; init; } = [];
    // Retained for schema-v1 compatibility. These legacy collections are no longer shown in Project Workspace.
    public List<ProjectTodoItem> Todos { get; init; } = [];
    public List<PlanningSnapshot> PlanningSnapshots { get; init; } = [];
    public ProjectFolderReference? WorkFolder { get; set; }
    public bool IsArchived { get; set; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public required DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset? ArchivedAtUtc { get; set; }

    private void EnsureDesignDetails()
    {
        if (Kind == ProjectKindCodes.Design) DesignDetails ??= new();
    }
}

public sealed class ProjectMilestone
{
    public required Guid Id { get; init; }
    public required string Title { get; set; }
    public required DateOnly Date { get; set; }
    public TimeOnly? Time { get; set; }
    // Missing values from v3 project files intentionally deserialize as true.
    public bool ReminderEnabled { get; set; } = true;
    public string? Notes { get; set; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public required DateTimeOffset UpdatedAtUtc { get; set; }
    public int DisplayOrder { get; set; }
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
    public string Kind { get; set; } = ProjectKindCodes.Design;
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

public sealed record ProjectDeleteResult(DataStorageStatus Status, string? FailureType = null)
{
    public bool Succeeded => Status == DataStorageStatus.Success;
}
