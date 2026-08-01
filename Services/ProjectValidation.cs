using UrbanPlanToolbox.Models.Projects;

namespace UrbanPlanToolbox.Services;

public static class ProjectValidation
{
    public const int MaxNameLength = 160;
    public const int MaxTypeLength = 80;
    public const int MaxAdministrativeAreaLength = 300;
    public const int MaxDescriptionLength = 10_000;
    public const int MaxPlanningRequirementsLength = 20_000;
    public const int MaxMilestoneTitleLength = 300;
    public const int MaxMilestoneNotesLength = 5_000;
    public const int MaxTodoTitleLength = 500;
    public const int MaxSnapshotNameLength = 160;

    public static IReadOnlyList<string> Validate(ProjectRecord project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var errors = new List<string>();
        if (project.Id == Guid.Empty) errors.Add("ProjectIdRequired");
        ValidateRequiredText(project.Name, MaxNameLength, "ProjectName", errors);
        if (!ProjectTypeCodes.IsValid(project.Type)) errors.Add("ProjectTypeInvalid");
        if (project.Type == ProjectTypeCodes.Other)
            ValidateRequiredText(project.CustomType, MaxTypeLength, "CustomProjectType", errors);
        else if (!string.IsNullOrWhiteSpace(project.CustomType))
            errors.Add("CustomProjectTypeOnlyForOther");
        ValidateOptionalText(project.AdministrativeArea, MaxAdministrativeAreaLength, "AdministrativeArea", errors);
        ValidateOptionalText(project.Description, MaxDescriptionLength, "ProjectDescription", errors);
        ValidateOptionalText(project.PlanningRequirements, MaxPlanningRequirementsLength, "PlanningRequirements", errors);

        if (project.Latitude.HasValue != project.Longitude.HasValue) errors.Add("CoordinatesMustBePaired");
        if (project.Latitude is < -90m or > 90m) errors.Add("LatitudeOutOfRange");
        if (project.Longitude is < -180m or > 180m) errors.Add("LongitudeOutOfRange");
        if (project.CreatedAtUtc.Offset != TimeSpan.Zero || project.UpdatedAtUtc.Offset != TimeSpan.Zero ||
            project.ArchivedAtUtc.HasValue && project.ArchivedAtUtc.Value.Offset != TimeSpan.Zero) errors.Add("ProjectTimestampsMustBeUtc");
        if (project.IsArchived != project.ArchivedAtUtc.HasValue) errors.Add("ArchiveStateInvalid");

        foreach (var todo in project.Todos)
        {
            if (todo.Id == Guid.Empty) errors.Add("TodoIdRequired");
            ValidateRequiredText(todo.Title, MaxTodoTitleLength, "TodoTitle", errors);
            if (todo.CreatedAtUtc.Offset != TimeSpan.Zero || todo.CompletedAtUtc.HasValue && todo.CompletedAtUtc.Value.Offset != TimeSpan.Zero ||
                todo.IsCompleted != todo.CompletedAtUtc.HasValue) errors.Add("TodoStateInvalid");
        }

        if (project.Todos.Select(item => item.Id).Distinct().Count() != project.Todos.Count) errors.Add("DuplicateTodoId");
        if (project.PlanningSnapshots.Select(item => item.Id).Distinct().Count() != project.PlanningSnapshots.Count) errors.Add("DuplicateSnapshotId");
        foreach (var snapshot in project.PlanningSnapshots)
        {
            if (snapshot.Id == Guid.Empty) errors.Add("SnapshotIdRequired");
            ValidateOptionalText(snapshot.Name, MaxSnapshotNameLength, "SnapshotName", errors);
            if (snapshot.CreatedAtUtc.Offset != TimeSpan.Zero || string.IsNullOrWhiteSpace(snapshot.CalculationModel)) errors.Add("SnapshotMetadataInvalid");
        }

        if (project.Milestones.Select(item => item.Id).Distinct().Count() != project.Milestones.Count) errors.Add("DuplicateMilestoneId");
        foreach (var milestone in project.Milestones)
        {
            if (milestone.Id == Guid.Empty) errors.Add("MilestoneIdRequired");
            ValidateRequiredText(milestone.Title, MaxMilestoneTitleLength, "MilestoneTitle", errors);
            ValidateOptionalText(milestone.Notes, MaxMilestoneNotesLength, "MilestoneNotes", errors);
            if (milestone.Date == default) errors.Add("MilestoneDateInvalid");
            if (milestone.CreatedAtUtc.Offset != TimeSpan.Zero || milestone.UpdatedAtUtc.Offset != TimeSpan.Zero)
                errors.Add("MilestoneTimestampsMustBeUtc");
        }

        return errors.Distinct(StringComparer.Ordinal).ToArray();
    }

    public static string NormalizeRequired(string value) => value.Trim();
    public static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    public static bool MatchesDeleteConfirmation(string projectName, string? confirmation) =>
        string.Equals(projectName, confirmation?.Trim(), StringComparison.Ordinal);

    private static void ValidateRequiredText(string? value, int maxLength, string key, ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value)) errors.Add($"{key}Required");
        else if (value.Trim().Length > maxLength) errors.Add($"{key}TooLong");
    }

    private static void ValidateOptionalText(string? value, int maxLength, string key, ICollection<string> errors)
    {
        if (value?.Trim().Length > maxLength) errors.Add($"{key}TooLong");
    }
}
