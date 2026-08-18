using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Models.Projects;

namespace UrbanPlanToolbox.Services;

public sealed class ProjectStorageService
{
    public const int ProjectSchemaVersion = DataContractVersions.Project;
    private const string IndexStorageId = "projects:index";
    private readonly IAppDataPathProvider _paths;
    private readonly JsonDataStorage _storage;
    private readonly Func<string, bool>? _deleteFailureInjector;
    private readonly SemaphoreSlim _mutationLock = new(1, 1);

    public static ProjectStorageService Default { get; } = new(AppDataPathProvider.Default);

    public ProjectStorageService(
        IAppDataPathProvider paths,
        IEnumerable<IDataMigration>? projectMigrations = null,
        IStorageDiagnostics? diagnostics = null,
        Func<string, bool>? deleteFailureInjector = null)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _storage = new JsonDataStorage(
            paths,
            ProjectSchemaVersion,
            projectMigrations ?? [new ProjectV1ToV2Migration(), new ProjectV2ToV3Migration(), new ProjectV3ToV4Migration()],
            diagnostics,
            allowUnversionedLegacySchema: true);
        _deleteFailureInjector = deleteFailureInjector;
    }

    public async Task<ProjectSaveResult> CreateAsync(
        string name,
        string type,
        string? customType = null,
        string? administrativeArea = null,
        decimal? latitude = null,
        decimal? longitude = null,
        string? description = null,
        string? planningRequirements = null,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var project = new ProjectRecord
        {
            Id = Guid.NewGuid(), Kind = ProjectKindCodes.Design, Name = ProjectValidation.NormalizeRequired(name), Type = type,
            CustomType = ProjectValidation.NormalizeOptional(customType),
            DesignDetails = new()
            {
                AdministrativeRegion = ProjectValidation.NormalizeOptional(administrativeArea),
                Latitude = latitude, Longitude = longitude,
                Description = ProjectValidation.NormalizeOptional(description),
                PlanningRequirements = ProjectValidation.NormalizeOptional(planningRequirements)
            },
            CreatedAtUtc = now, UpdatedAtUtc = now
        };
        return await SaveAsync(project, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProjectSaveResult> CreateResearchAsync(
        string name, string type, string? customType, string? researchField,
        string? researchSubject, string? researchMethods,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var project = new ProjectRecord
        {
            Id = Guid.NewGuid(), Kind = ProjectKindCodes.Research,
            Name = ProjectValidation.NormalizeRequired(name), Type = type,
            CustomType = ProjectValidation.NormalizeOptional(customType),
            ResearchDetails = new()
            {
                ResearchField = ProjectValidation.NormalizeOptional(researchField),
                ResearchSubject = ProjectValidation.NormalizeOptional(researchSubject),
                ResearchMethods = ProjectValidation.NormalizeOptional(researchMethods)
            },
            CreatedAtUtc = now, UpdatedAtUtc = now
        };
        return await SaveAsync(project, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProjectSaveResult> SaveAsync(ProjectRecord project, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        project.Name = ProjectValidation.NormalizeRequired(project.Name);
        project.CustomType = ProjectValidation.NormalizeOptional(project.CustomType);
        if (project.Kind == ProjectKindCodes.Design && project.DesignDetails is not null)
        {
            project.DesignDetails.AdministrativeRegion = ProjectValidation.NormalizeOptional(project.DesignDetails.AdministrativeRegion);
            project.DesignDetails.Description = ProjectValidation.NormalizeOptional(project.DesignDetails.Description);
            project.DesignDetails.PlanningRequirements = ProjectValidation.NormalizeOptional(project.DesignDetails.PlanningRequirements);
        }
        if (project.Kind == ProjectKindCodes.Research && project.ResearchDetails is not null)
        {
            project.ResearchDetails.ResearchField = ProjectValidation.NormalizeOptional(project.ResearchDetails.ResearchField);
            project.ResearchDetails.ResearchSubject = ProjectValidation.NormalizeOptional(project.ResearchDetails.ResearchSubject);
            project.ResearchDetails.ResearchMethods = ProjectValidation.NormalizeOptional(project.ResearchDetails.ResearchMethods);
        }
        foreach (var milestone in project.Milestones)
        {
            milestone.Title = ProjectValidation.NormalizeRequired(milestone.Title);
            milestone.Notes = ProjectValidation.NormalizeOptional(milestone.Notes);
        }
        var validation = ProjectValidation.Validate(project);
        if (validation.Count > 0) return new(DataStorageStatus.Corrupt, ValidationErrors: validation);

        var existingPath = _paths.GetProjectDataFilePath(project.Id);
        if (File.Exists(existingPath))
        {
            var existing = await ReadAsync(project.Id, cancellationToken).ConfigureAwait(false);
            if (!existing.HasValue) return new(existing.Status, FailureType: existing.FailureType);
            if (!string.Equals(existing.Value!.Kind, project.Kind, StringComparison.Ordinal))
                return new(DataStorageStatus.Corrupt, ValidationErrors: ["ProjectKindImmutable"]);
        }

        await _mutationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var projectWrite = await _storage.SaveFileAsync(
                ProjectStorageId(project.Id), _paths.GetProjectDataFilePath(project.Id),
                _paths.GetProjectBackupFilePath(project.Id), project, cancellationToken).ConfigureAwait(false);
            if (!projectWrite.Succeeded) return new(projectWrite.Status, FailureType: projectWrite.FailureType);

            var indexRead = await ReadIndexAsync(cancellationToken).ConfigureAwait(false);
            if (indexRead.Status is not (DataStorageStatus.Success or DataStorageStatus.RecoveredFromBackup or DataStorageStatus.NotFound))
                return new(indexRead.Status, FailureType: indexRead.FailureType);
            var index = indexRead.Value ?? new ProjectIndex();
            var existing = index.Projects.FirstOrDefault(item => item.Id == project.Id);
            if (existing is null)
            {
                index.Projects.Add(ToIndexEntry(project));
            }
            else
            {
                existing.Kind = project.Kind; existing.Name = project.Name; existing.Type = project.Type; existing.IsArchived = project.IsArchived;
                existing.UpdatedAtUtc = project.UpdatedAtUtc; existing.ArchivedAtUtc = project.ArchivedAtUtc;
            }

            var indexWrite = await SaveIndexAsync(index, cancellationToken).ConfigureAwait(false);
            return indexWrite.Succeeded
                ? new(DataStorageStatus.Success, project)
                : new(indexWrite.Status, FailureType: indexWrite.FailureType);
        }
        finally { _mutationLock.Release(); }
    }

    public Task<DataReadResult<ProjectRecord>> ReadAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        _storage.ReadFileAsync<ProjectRecord>(ProjectStorageId(projectId), "project.json",
            _paths.GetProjectDataFilePath(projectId), _paths.GetProjectBackupFilePath(projectId), cancellationToken);

    public async Task<ProjectListResult> ListAsync(bool archived, CancellationToken cancellationToken = default)
    {
        var indexRead = await ReadIndexAsync(cancellationToken).ConfigureAwait(false);
        if (indexRead.Status == DataStorageStatus.NotFound) return new([], []);
        if (!indexRead.HasValue) return new([], [new(Guid.Empty, indexRead.Status, indexRead.FailureType)]);

        var projects = new List<ProjectRecord>();
        var issues = new List<ProjectIssue>();
        foreach (var item in indexRead.Value!.Projects.Where(item => item.IsArchived == archived))
        {
            var read = await ReadAsync(item.Id, cancellationToken).ConfigureAwait(false);
            if (read.HasValue && read.Value!.Id == item.Id) projects.Add(read.Value);
            else issues.Add(new(item.Id, read.Status, read.FailureType));
        }

        var ordered = archived
            ? projects.OrderByDescending(project => project.ArchivedAtUtc ?? project.UpdatedAtUtc)
            : projects.OrderByDescending(project => project.UpdatedAtUtc);
        return new(ordered.ToArray(), issues);
    }

    public async Task<ProjectSaveResult> ArchiveAsync(Guid projectId, bool archived, CancellationToken cancellationToken = default)
    {
        var read = await ReadAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (!read.HasValue) return new(read.Status, FailureType: read.FailureType);
        read.Value!.IsArchived = archived;
        read.Value.ArchivedAtUtc = archived ? DateTimeOffset.UtcNow : null;
        read.Value.UpdatedAtUtc = DateTimeOffset.UtcNow;
        return await SaveAsync(read.Value, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProjectSaveResult> AddMilestoneAsync(
        Guid projectId, string title, DateOnly date, TimeOnly? time = null, string? notes = null,
        CancellationToken cancellationToken = default, bool reminderEnabled = true)
    {
        var read = await ReadEditableAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (!read.HasValue) return new(read.Status, FailureType: read.FailureType);
        var now = DateTimeOffset.UtcNow;
        read.Value!.Milestones.Add(new ProjectMilestone
        {
            Id = Guid.NewGuid(), Title = ProjectValidation.NormalizeRequired(title), Date = date, Time = time,
            ReminderEnabled = reminderEnabled,
            Notes = ProjectValidation.NormalizeOptional(notes), CreatedAtUtc = now, UpdatedAtUtc = now,
            DisplayOrder = read.Value.Milestones.Count
        });
        read.Value.UpdatedAtUtc = now;
        return await SaveAsync(read.Value, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProjectSaveResult> UpdateMilestoneAsync(
        Guid projectId, Guid milestoneId, string title, DateOnly date, TimeOnly? time = null, string? notes = null,
        CancellationToken cancellationToken = default, bool reminderEnabled = true)
    {
        var read = await ReadEditableAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (!read.HasValue) return new(read.Status, FailureType: read.FailureType);
        var milestone = read.Value!.Milestones.FirstOrDefault(item => item.Id == milestoneId);
        if (milestone is null) return new(DataStorageStatus.NotFound, FailureType: "MilestoneNotFound");
        milestone.Title = ProjectValidation.NormalizeRequired(title);
        milestone.Date = date;
        milestone.Time = time;
        milestone.ReminderEnabled = reminderEnabled;
        milestone.Notes = ProjectValidation.NormalizeOptional(notes);
        milestone.UpdatedAtUtc = DateTimeOffset.UtcNow;
        read.Value.UpdatedAtUtc = milestone.UpdatedAtUtc;
        return await SaveAsync(read.Value, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProjectSaveResult> DeleteMilestoneAsync(
        Guid projectId, Guid milestoneId, CancellationToken cancellationToken = default)
    {
        var read = await ReadEditableAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (!read.HasValue) return new(read.Status, FailureType: read.FailureType);
        if (read.Value!.Milestones.RemoveAll(item => item.Id == milestoneId) == 0)
            return new(DataStorageStatus.NotFound, FailureType: "MilestoneNotFound");
        for (var index = 0; index < read.Value.Milestones.Count; index++)
            read.Value.Milestones[index].DisplayOrder = index;
        read.Value.UpdatedAtUtc = DateTimeOffset.UtcNow;
        return await SaveAsync(read.Value, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProjectDeleteResult> DeleteAsync(
        Guid projectId, IProjectFolderAccessService? folderAccess = null,
        CancellationToken cancellationToken = default)
    {
        var read = await ReadAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (!read.HasValue) return new(read.Status, read.FailureType);

        await _mutationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        var tombstone = Path.Combine(_paths.Paths.RootDirectory, $".delete-{projectId:N}-{Guid.NewGuid():N}");
        var moved = new List<(string Source, string Destination)>();
        ProjectIndex? indexToRestore = null;
        ProjectIndexEntry? removedEntry = null;
        try
        {
            var indexRead = await ReadIndexAsync(cancellationToken).ConfigureAwait(false);
            if (!indexRead.HasValue) return new(indexRead.Status, indexRead.FailureType);
            if (_deleteFailureInjector?.Invoke("BeforeStage") == true) throw new IOException("InjectedDeleteFailure");

            Directory.CreateDirectory(tombstone);
            StageDirectory(Path.Combine(_paths.Paths.ProjectsDirectory, projectId.ToString("D")), Path.Combine(tombstone, "data"), moved);
            StageDirectory(Path.Combine(_paths.Paths.ProjectBackupsDirectory, projectId.ToString("D")), Path.Combine(tombstone, "backups"), moved);
            StageDirectory(Path.Combine(_paths.Paths.ProjectAttachmentsDirectory, projectId.ToString("D")), Path.Combine(tombstone, "attachments"), moved);
            if (_deleteFailureInjector?.Invoke("AfterStage") == true) throw new IOException("InjectedDeleteFailure");

            var index = indexRead.Value!;
            removedEntry = index.Projects.FirstOrDefault(item => item.Id == projectId);
            if (removedEntry is null)
            {
                RestoreStagedDirectories(moved);
                return new(DataStorageStatus.NotFound, "ProjectIndexEntryNotFound");
            }
            index.Projects.Remove(removedEntry);

            var indexWrite = await SaveIndexAsync(index, cancellationToken).ConfigureAwait(false);
            if (!indexWrite.Succeeded)
            {
                RestoreStagedDirectories(moved);
                return new(indexWrite.Status, indexWrite.FailureType);
            }
            indexToRestore = index;
            if (_deleteFailureInjector?.Invoke("AfterIndex") == true) throw new IOException("InjectedDeleteFailure");

            Directory.Delete(tombstone, recursive: true);

            try
            {
                folderAccess?.Clear(read.Value!.WorkFolder);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                // Project data is already gone. A stale platform token must not
                // turn the completed deletion into an unrecoverable half-failure.
            }
            return new(DataStorageStatus.Success);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            RestoreStagedDirectories(moved);
            if (indexToRestore is not null && removedEntry is not null && indexToRestore.Projects.All(item => item.Id != removedEntry.Id))
            {
                indexToRestore.Projects.Add(removedEntry);
                await SaveIndexAsync(indexToRestore, cancellationToken).ConfigureAwait(false);
            }
            TryDeleteDirectory(tombstone);
            return new(DataStorageStatus.IoFailure, exception.GetType().Name);
        }
        finally { _mutationLock.Release(); }
    }

    public async Task<ProjectSaveResult> AddTodoAsync(Guid projectId, string title, CancellationToken cancellationToken = default)
    {
        var read = await ReadEditableAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (!read.HasValue) return new(read.Status, FailureType: read.FailureType);
        var normalized = ProjectValidation.NormalizeRequired(title);
        var now = DateTimeOffset.UtcNow;
        read.Value!.Todos.Add(new ProjectTodoItem { Id = Guid.NewGuid(), Title = normalized, CreatedAtUtc = now, DisplayOrder = read.Value.Todos.Count });
        read.Value.UpdatedAtUtc = now;
        return await SaveAsync(read.Value, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProjectSaveResult> UpdateTodoAsync(Guid projectId, Guid todoId, string? title = null, bool? isCompleted = null, CancellationToken cancellationToken = default)
    {
        var read = await ReadEditableAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (!read.HasValue) return new(read.Status, FailureType: read.FailureType);
        var todo = read.Value!.Todos.FirstOrDefault(item => item.Id == todoId);
        if (todo is null) return new(DataStorageStatus.NotFound, FailureType: "TodoNotFound");
        if (title is not null) todo.Title = ProjectValidation.NormalizeRequired(title);
        if (isCompleted.HasValue)
        {
            todo.IsCompleted = isCompleted.Value;
            todo.CompletedAtUtc = isCompleted.Value ? DateTimeOffset.UtcNow : null;
        }
        read.Value.UpdatedAtUtc = DateTimeOffset.UtcNow;
        return await SaveAsync(read.Value, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProjectSaveResult> DeleteTodoAsync(Guid projectId, Guid todoId, CancellationToken cancellationToken = default)
    {
        var read = await ReadEditableAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (!read.HasValue) return new(read.Status, FailureType: read.FailureType);
        if (read.Value!.Todos.RemoveAll(item => item.Id == todoId) == 0) return new(DataStorageStatus.NotFound, FailureType: "TodoNotFound");
        for (var index = 0; index < read.Value.Todos.Count; index++) read.Value.Todos[index].DisplayOrder = index;
        read.Value.UpdatedAtUtc = DateTimeOffset.UtcNow;
        return await SaveAsync(read.Value, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProjectSaveResult> AddSnapshotAsync(Guid projectId, PlanningInput input, PlanningResult result, string? name = null, CancellationToken cancellationToken = default)
    {
        if (result.Errors.Count > 0) return new(DataStorageStatus.Corrupt, ValidationErrors: ["PlanningResultInvalid"]);
        var read = await ReadEditableAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (!read.HasValue) return new(read.Status, FailureType: read.FailureType);
        read.Value!.PlanningSnapshots.Add(new PlanningSnapshot
        {
            Id = Guid.NewGuid(), CreatedAtUtc = DateTimeOffset.UtcNow,
            Name = ProjectValidation.NormalizeOptional(name), Input = input, Result = result
        });
        read.Value.UpdatedAtUtc = DateTimeOffset.UtcNow;
        return await SaveAsync(read.Value, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProjectSaveResult> DeleteSnapshotAsync(Guid projectId, Guid snapshotId, CancellationToken cancellationToken = default)
    {
        var read = await ReadEditableAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (!read.HasValue) return new(read.Status, FailureType: read.FailureType);
        if (read.Value!.PlanningSnapshots.RemoveAll(item => item.Id == snapshotId) == 0) return new(DataStorageStatus.NotFound, FailureType: "SnapshotNotFound");
        read.Value.UpdatedAtUtc = DateTimeOffset.UtcNow;
        return await SaveAsync(read.Value, cancellationToken).ConfigureAwait(false);
    }

    private async Task<DataReadResult<ProjectRecord>> ReadEditableAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var read = await ReadAsync(projectId, cancellationToken).ConfigureAwait(false);
        return read.HasValue && read.Value!.IsArchived
            ? new(DataStorageStatus.IoFailure, FailureType: "ArchivedProjectReadOnly")
            : read;
    }

    private Task<DataReadResult<ProjectIndex>> ReadIndexAsync(CancellationToken cancellationToken) =>
        _storage.ReadFileAsync<ProjectIndex>(IndexStorageId, "index.json", _paths.GetProjectsIndexFilePath(), _paths.GetProjectsIndexBackupFilePath(), cancellationToken);

    private Task<DataWriteResult> SaveIndexAsync(ProjectIndex index, CancellationToken cancellationToken) =>
        _storage.SaveFileAsync(IndexStorageId, _paths.GetProjectsIndexFilePath(), _paths.GetProjectsIndexBackupFilePath(), index, cancellationToken);

    private static string ProjectStorageId(Guid projectId)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("Project ID cannot be empty.", nameof(projectId));
        return $"projects:{projectId:D}";
    }

    private static ProjectIndexEntry ToIndexEntry(ProjectRecord project) => new()
    {
        Id = project.Id, Kind = project.Kind, Name = project.Name, Type = project.Type, IsArchived = project.IsArchived,
        UpdatedAtUtc = project.UpdatedAtUtc, ArchivedAtUtc = project.ArchivedAtUtc
    };

    private static void StageDirectory(string source, string destination, ICollection<(string Source, string Destination)> moved)
    {
        if (!Directory.Exists(source)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        Directory.Move(source, destination);
        moved.Add((source, destination));
    }

    private static void RestoreStagedDirectories(IEnumerable<(string Source, string Destination)> moved)
    {
        foreach (var item in moved.Reverse())
        {
            if (!Directory.Exists(item.Destination) || Directory.Exists(item.Source)) continue;
            Directory.CreateDirectory(Path.GetDirectoryName(item.Source)!);
            Directory.Move(item.Destination, item.Source);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}