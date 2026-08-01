using System.Globalization;
using System.Text;
using System.Text.Json;
using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Models.Projects;
using UrbanPlanToolbox.Models.Tools;
using UrbanPlanToolbox.Services;
using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class ProjectStorageTests
{
    [Fact]
    public async Task CreateReadAndEditKeepStableIdAndIdBasedDirectory()
    {
        using var scope = new ProjectScope();
        var created = await scope.Service.CreateAsync("  Canal renewal  ", ProjectTypeCodes.Research);
        var id = created.Project!.Id;
        created.Project.Name = "Renamed";
        created.Project.UpdatedAtUtc = DateTimeOffset.UtcNow;
        var saved = await scope.Service.SaveAsync(created.Project);
        var read = await scope.Service.ReadAsync(id);

        Assert.True(saved.Succeeded);
        Assert.Equal(id, read.Value!.Id);
        Assert.Equal("Renamed", read.Value.Name);
        Assert.Equal(Path.Combine(scope.Provider.Paths.ProjectsDirectory, id.ToString("D"), "project.json"), scope.Provider.GetProjectDataFilePath(id));
    }

    [Fact]
    public async Task DuplicateNamesCreateDifferentProjects()
    {
        using var scope = new ProjectScope();
        var first = await scope.Service.CreateAsync("Same", ProjectTypeCodes.Coursework);
        var second = await scope.Service.CreateAsync("Same", ProjectTypeCodes.Coursework);
        Assert.NotEqual(first.Project!.Id, second.Project!.Id);
        Assert.Equal(2, (await scope.Service.ListAsync(false)).Projects.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task BlankProjectNamesAreRejectedWithoutCreatingDirectory(string name)
    {
        using var scope = new ProjectScope();
        var result = await scope.Service.CreateAsync(name, ProjectTypeCodes.Coursework);
        Assert.False(result.Succeeded);
        Assert.Contains("ProjectNameRequired", result.ValidationErrors!);
        Assert.Empty(Directory.GetDirectories(scope.Provider.Paths.ProjectsDirectory));
    }

    [Theory]
    [InlineData(-90, -180)]
    [InlineData(90, 180)]
    [InlineData(30.1, 120.2)]
    public async Task ValidCoordinatesRoundTrip(double latitude, double longitude)
    {
        using var scope = new ProjectScope();
        var result = await scope.Service.CreateAsync("Coordinates", ProjectTypeCodes.Research, latitude: (decimal)latitude, longitude: (decimal)longitude);
        var read = await scope.Service.ReadAsync(result.Project!.Id);
        Assert.Equal((decimal)latitude, read.Value!.Latitude);
        Assert.Equal((decimal)longitude, read.Value.Longitude);
    }

    [Theory]
    [InlineData(-90.1, 0, "LatitudeOutOfRange")]
    [InlineData(90.1, 0, "LatitudeOutOfRange")]
    [InlineData(0, -180.1, "LongitudeOutOfRange")]
    [InlineData(0, 180.1, "LongitudeOutOfRange")]
    public async Task InvalidCoordinatesAreRejected(double latitude, double longitude, string error)
    {
        using var scope = new ProjectScope();
        var result = await scope.Service.CreateAsync("Coordinates", ProjectTypeCodes.Research, latitude: (decimal)latitude, longitude: (decimal)longitude);
        Assert.Contains(error, result.ValidationErrors!);
    }

    [Fact]
    public async Task CoordinatesMustBeProvidedAsPair()
    {
        using var scope = new ProjectScope();
        var result = await scope.Service.CreateAsync("Coordinates", ProjectTypeCodes.Research, latitude: 30m);
        Assert.Contains("CoordinatesMustBePaired", result.ValidationErrors!);
    }

    [Fact]
    public async Task OtherRequiresCustomTypeAndStableTypeDoesNotDependOnDisplayLanguage()
    {
        using var scope = new ProjectScope();
        var invalid = await scope.Service.CreateAsync("Other", ProjectTypeCodes.Other);
        var valid = await scope.Service.CreateAsync("Other", ProjectTypeCodes.Other, "Field study");
        Assert.Contains("CustomProjectTypeRequired", invalid.ValidationErrors!);
        Assert.Equal("other", (await scope.Service.ReadAsync(valid.Project!.Id)).Value!.Type);
    }

    [Fact]
    public async Task DecimalPersistenceIsCultureIndependent()
    {
        using var scope = new ProjectScope();
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            var created = await scope.Service.CreateAsync("Culture", ProjectTypeCodes.Research, latitude: 30.25m, longitude: 120.5m);
            var json = await File.ReadAllTextAsync(scope.Provider.GetProjectDataFilePath(created.Project!.Id), Encoding.UTF8);
            Assert.Contains("30.25", json);
            Assert.DoesNotContain("30,25", json);
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    [Fact]
    public async Task IndexContainsSummaryButNotProjectBody()
    {
        using var scope = new ProjectScope();
        var created = await scope.Service.CreateAsync("Indexed", ProjectTypeCodes.Research, description: "secret-body-marker");
        await scope.Service.AddTodoAsync(created.Project!.Id, "secret-todo-marker");
        var json = await File.ReadAllTextAsync(scope.Provider.GetProjectsIndexFilePath(), Encoding.UTF8);
        Assert.Contains("Indexed", json);
        Assert.DoesNotContain("secret-body-marker", json);
        Assert.DoesNotContain("secret-todo-marker", json);
        Assert.DoesNotContain("planningSnapshots", json);
    }

    [Fact]
    public async Task ListIsSortedByUpdatedTimeDescending()
    {
        using var scope = new ProjectScope();
        var first = (await scope.Service.CreateAsync("First", ProjectTypeCodes.Research)).Project!;
        var second = (await scope.Service.CreateAsync("Second", ProjectTypeCodes.Research)).Project!;
        first.UpdatedAtUtc = second.UpdatedAtUtc.AddMinutes(1);
        await scope.Service.SaveAsync(first);
        Assert.Equal(["First", "Second"], (await scope.Service.ListAsync(false)).Projects.Select(item => item.Name));
    }

    [Fact]
    public async Task CorruptProjectDoesNotBlockOtherProjects()
    {
        using var scope = new ProjectScope();
        var healthy = (await scope.Service.CreateAsync("Healthy", ProjectTypeCodes.Research)).Project!;
        var corrupt = (await scope.Service.CreateAsync("Corrupt", ProjectTypeCodes.Research)).Project!;
        await File.WriteAllTextAsync(scope.Provider.GetProjectDataFilePath(corrupt.Id), "{broken", Encoding.UTF8);
        var list = await scope.Service.ListAsync(false);
        Assert.Single(list.Projects);
        Assert.Equal(healthy.Id, list.Projects[0].Id);
        Assert.Single(list.Issues);
        Assert.Equal(corrupt.Id, list.Issues[0].ProjectId);
    }

    [Fact]
    public async Task ProjectRecoversFromLastValidBackup()
    {
        using var scope = new ProjectScope();
        var project = (await scope.Service.CreateAsync("First", ProjectTypeCodes.Research)).Project!;
        project.Name = "Second"; project.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await scope.Service.SaveAsync(project);
        await File.WriteAllTextAsync(scope.Provider.GetProjectDataFilePath(project.Id), "broken", Encoding.UTF8);
        var recovered = await scope.Service.ReadAsync(project.Id);
        Assert.Equal(DataStorageStatus.RecoveredFromBackup, recovered.Status);
        Assert.Equal("First", recovered.Value!.Name);
    }

    [Fact]
    public async Task FutureProjectVersionCannotBeReadOrOverwritten()
    {
        using var scope = new ProjectScope();
        var id = Guid.NewGuid();
        var path = scope.Provider.GetProjectDataFilePath(id);
        var future = new DataEnvelope<object> { SchemaVersion = 4, SavedAtUtc = DateTimeOffset.UtcNow, Payload = new { id, name = "Future" } };
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(future, DataStorageJson.Options), new UTF8Encoding(false));
        var before = await File.ReadAllBytesAsync(path);
        var read = await scope.Service.ReadAsync(id);
        var save = await scope.Service.SaveAsync(SampleProject(id, "Old app"));
        Assert.Equal(DataStorageStatus.UnsupportedFutureVersion, read.Status);
        Assert.Equal(DataStorageStatus.UnsupportedFutureVersion, save.Status);
        Assert.Equal(before, await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task TodoLifecyclePreservesIdOrderAndStatistics()
    {
        using var scope = new ProjectScope();
        var project = (await scope.Service.CreateAsync("Todos", ProjectTypeCodes.Coursework)).Project!;
        await scope.Service.AddTodoAsync(project.Id, "One");
        var two = await scope.Service.AddTodoAsync(project.Id, "Two");
        var firstId = two.Project!.Todos[0].Id;
        var secondId = two.Project.Todos[1].Id;
        await scope.Service.UpdateTodoAsync(project.Id, firstId, "One edited", true);
        await scope.Service.UpdateTodoAsync(project.Id, firstId, isCompleted: false);
        await scope.Service.DeleteTodoAsync(project.Id, firstId);
        var read = (await scope.Service.ReadAsync(project.Id)).Value!;
        Assert.Single(read.Todos);
        Assert.Equal(secondId, read.Todos[0].Id);
        Assert.Equal(0, read.Todos[0].DisplayOrder);
        Assert.Equal(0, read.Todos.Count(item => item.IsCompleted));
    }

    [Fact]
    public async Task ArchivedProjectsAreFilteredReadOnlyAndRestorable()
    {
        using var scope = new ProjectScope();
        var project = (await scope.Service.CreateAsync("Archive", ProjectTypeCodes.Professional)).Project!;
        await scope.Service.ArchiveAsync(project.Id, true);
        Assert.Empty((await scope.Service.ListAsync(false)).Projects);
        Assert.Single((await scope.Service.ListAsync(true)).Projects);
        Assert.Equal("ArchivedProjectReadOnly", (await scope.Service.AddTodoAsync(project.Id, "blocked")).FailureType);
        await scope.Service.ArchiveAsync(project.Id, false);
        Assert.Single((await scope.Service.ListAsync(false)).Projects);
        Assert.Null((await scope.Service.ReadAsync(project.Id)).Value!.ArchivedAtUtc);
    }

    [Fact]
    public async Task ArchivedProjectsAreSortedByArchiveTimeDescending()
    {
        using var scope = new ProjectScope();
        var first = (await scope.Service.CreateAsync("First", ProjectTypeCodes.Research)).Project!;
        var second = (await scope.Service.CreateAsync("Second", ProjectTypeCodes.Research)).Project!;
        await scope.Service.ArchiveAsync(first.Id, true);
        await scope.Service.ArchiveAsync(second.Id, true);
        first = (await scope.Service.ReadAsync(first.Id)).Value!;
        first.ArchivedAtUtc = DateTimeOffset.UtcNow.AddHours(1);
        await scope.Service.SaveAsync(first);
        Assert.Equal(["First", "Second"], (await scope.Service.ListAsync(true)).Projects.Select(item => item.Name));
    }

    [Fact]
    public async Task PlanningRequirementsAndLegacyCollectionsRoundTripTogether()
    {
        using var scope = new ProjectScope();
        var project = (await scope.Service.CreateAsync("Requirements", ProjectTypeCodes.Research)).Project!;
        await scope.Service.AddTodoAsync(project.Id, "legacy todo");
        var current = (await scope.Service.ReadAsync(project.Id)).Value!;
        current.PlanningRequirements = "  Protect the canal edge.  ";
        current.UpdatedAtUtc = DateTimeOffset.UtcNow;

        var saved = await scope.Service.SaveAsync(current);
        var read = (await scope.Service.ReadAsync(project.Id)).Value!;

        Assert.True(saved.Succeeded);
        Assert.Equal("Protect the canal edge.", read.PlanningRequirements);
        Assert.Single(read.Todos);
        Assert.Equal("legacy todo", read.Todos[0].Title);
    }

    [Fact]
    public async Task MilestoneLifecycleKeepsStableIdOrderAndArchivedReadOnlyState()
    {
        using var scope = new ProjectScope();
        var project = (await scope.Service.CreateAsync("Milestones", ProjectTypeCodes.Professional)).Project!;
        var invalid = await scope.Service.AddMilestoneAsync(project.Id, "   ", new DateOnly(2026, 8, 2));
        Assert.Contains("MilestoneTitleRequired", invalid.ValidationErrors!);
        Assert.Empty((await scope.Service.ReadAsync(project.Id)).Value!.Milestones);

        var first = await scope.Service.AddMilestoneAsync(project.Id, "Review", new DateOnly(2026, 8, 2), new TimeOnly(9, 30), "Room A");
        var firstId = first.Project!.Milestones[0].Id;
        var second = await scope.Service.AddMilestoneAsync(project.Id, "Submission", new DateOnly(2026, 8, 5));
        var secondId = second.Project!.Milestones[1].Id;
        var edited = await scope.Service.UpdateMilestoneAsync(project.Id, firstId, "Public review", new DateOnly(2026, 8, 3), notes: "Updated");
        Assert.Equal(firstId, edited.Project!.Milestones[0].Id);
        Assert.Equal("Public review", edited.Project.Milestones[0].Title);
        Assert.Null(edited.Project.Milestones[0].Time);

        await scope.Service.ArchiveAsync(project.Id, true);
        Assert.Equal("ArchivedProjectReadOnly", (await scope.Service.DeleteMilestoneAsync(project.Id, firstId)).FailureType);
        await scope.Service.ArchiveAsync(project.Id, false);
        await scope.Service.DeleteMilestoneAsync(project.Id, firstId);
        var read = (await scope.Service.ReadAsync(project.Id)).Value!;
        Assert.Single(read.Milestones);
        Assert.Equal(secondId, read.Milestones[0].Id);
        Assert.Equal(0, read.Milestones[0].DisplayOrder);
    }

    [Fact]
    public async Task SchemaOneProjectMigratesThroughThreeWithoutLosingLegacyData()
    {
        using var scope = new ProjectScope();
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var legacy = new DataEnvelope<object>
        {
            SchemaVersion = 1,
            SavedAtUtc = now,
            Payload = new
            {
                id, name = "Legacy", type = ProjectTypeCodes.Research,
                todos = new[] { new { id = Guid.NewGuid(), title = "Keep me", isCompleted = false, createdAtUtc = now, completedAtUtc = (DateTimeOffset?)null, displayOrder = 0 } },
                planningSnapshots = Array.Empty<object>(), isArchived = false,
                createdAtUtc = now, updatedAtUtc = now, archivedAtUtc = (DateTimeOffset?)null
            }
        };
        var path = scope.Provider.GetProjectDataFilePath(id);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(legacy, DataStorageJson.Options), new UTF8Encoding(false));

        var read = await scope.Service.ReadAsync(id);
        var migratedJson = await File.ReadAllTextAsync(path);
        var backupJson = await File.ReadAllTextAsync(scope.Provider.GetProjectBackupFilePath(id));

        Assert.Equal(DataStorageStatus.Success, read.Status);
        Assert.Equal(3, read.SchemaVersion);
        Assert.Equal(ProjectKindCodes.Design, read.Value!.Kind);
        Assert.NotNull(read.Value.DesignDetails);
        Assert.Null(read.Value.ResearchDetails);
        Assert.Null(read.Value.PlanningRequirements);
        Assert.Empty(read.Value.Milestones);
        Assert.Single(read.Value.Todos);
        Assert.Contains("\"schemaVersion\": 3", migratedJson);
        Assert.Contains("\"kind\": \"design\"", migratedJson);
        Assert.Contains("\"designDetails\"", migratedJson);
        Assert.Contains("\"milestones\": []", migratedJson);
        Assert.Contains("\"schemaVersion\": 1", backupJson);
        Assert.Contains("Keep me", backupJson);
    }

    [Fact]
    public async Task MissingSchemaMigrationFailsWithoutChangingLegacyFile()
    {
        using var scope = new ProjectScope();
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var envelope = new DataEnvelope<object> { SchemaVersion = 1, SavedAtUtc = now, Payload = new { id, name = "Legacy", type = ProjectTypeCodes.Research, createdAtUtc = now, updatedAtUtc = now } };
        var path = scope.Provider.GetProjectDataFilePath(id);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(envelope, DataStorageJson.Options), new UTF8Encoding(false));
        var before = await File.ReadAllBytesAsync(path);
        var service = new ProjectStorageService(scope.Provider, Array.Empty<IDataMigration>());

        var read = await service.ReadAsync(id);

        Assert.Equal(DataStorageStatus.MigrationFailed, read.Status);
        Assert.Equal(before, await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task SchemaTwoProjectMigratesToDesignDetailsAndKeepsIdentityLifecycleAndFolder()
    {
        using var scope = new ProjectScope();
        var id = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        var milestoneId = Guid.NewGuid();
        var envelope = new DataEnvelope<object>
        {
            SchemaVersion = 2, SavedAtUtc = now,
            Payload = new
            {
                id, name = "Legacy design", type = ProjectTypeCodes.Competition, customType = (string?)null,
                administrativeArea = "Hangzhou", latitude = 30.25m, longitude = 120.5m,
                description = "Keep description", planningRequirements = "Keep requirements",
                milestones = new[] { new { id = milestoneId, title = "Review", date = "2026-08-04", time = (string?)null, notes = "Keep", createdAtUtc = now, updatedAtUtc = now, displayOrder = 0 } },
                todos = Array.Empty<object>(), planningSnapshots = Array.Empty<object>(),
                workFolder = new { accessToken = "token", displayName = "Work", displayPath = "C:\\Work", requiresReselection = false },
                isArchived = true, createdAtUtc = now, updatedAtUtc = now, archivedAtUtc = now
            }
        };
        var path = scope.Provider.GetProjectDataFilePath(id);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(envelope, DataStorageJson.Options), new UTF8Encoding(false));

        var read = await scope.Service.ReadAsync(id);

        Assert.Equal(3, read.SchemaVersion);
        Assert.Equal(ProjectKindCodes.Design, read.Value!.Kind);
        Assert.Equal(id, read.Value.Id);
        Assert.Equal("Hangzhou", read.Value.DesignDetails!.AdministrativeRegion);
        Assert.Equal("Keep requirements", read.Value.DesignDetails.PlanningRequirements);
        Assert.Equal(milestoneId, Assert.Single(read.Value.Milestones).Id);
        Assert.True(read.Value.IsArchived);
        Assert.Equal("token", read.Value.WorkFolder!.AccessToken);
        var migrated = await File.ReadAllTextAsync(path);
        Assert.DoesNotContain("\"administrativeArea\"", migrated);
        Assert.Contains("\"designDetails\"", migrated);
        Assert.Contains("\"researchDetails\": null", migrated);
    }

    [Fact]
    public async Task ResearchProjectRoundTripsAndRejectsDesignDataOrType()
    {
        using var scope = new ProjectScope();
        var created = await scope.Service.CreateResearchAsync("  Study  ", ResearchProjectTypeCodes.Thesis, null, " Planning ", " Canal districts ", " GIS and interviews ");
        Assert.True(created.Succeeded);
        Assert.Equal(ProjectKindCodes.Research, created.Project!.Kind);
        Assert.Null(created.Project.DesignDetails);
        Assert.Equal("Planning", created.Project.ResearchDetails!.ResearchField);

        created.Project.DesignDetails = new();
        Assert.Contains("DesignDetailsNotAllowed", (await scope.Service.SaveAsync(created.Project)).ValidationErrors!);
        created.Project.DesignDetails = null;
        created.Project.Type = ProjectTypeCodes.Competition;
        Assert.Contains("ProjectTypeInvalid", (await scope.Service.SaveAsync(created.Project)).ValidationErrors!);
    }

    [Fact]
    public async Task DesignProjectRejectsResearchDetailsAndResearchOnlyType()
    {
        using var scope = new ProjectScope();
        var project = (await scope.Service.CreateAsync("Design", ProjectTypeCodes.Professional)).Project!;
        project.ResearchDetails = new() { ResearchField = "F", ResearchSubject = "S", ResearchMethods = "M" };
        Assert.Contains("ResearchDetailsNotAllowed", (await scope.Service.SaveAsync(project)).ValidationErrors!);
        project.ResearchDetails = null;
        project.Type = ResearchProjectTypeCodes.Thesis;
        Assert.Contains("ProjectTypeInvalid", (await scope.Service.SaveAsync(project)).ValidationErrors!);
    }

    [Fact]
    public async Task ResearchOtherRequiresCustomTypeAndArchivedLifecycleStaysResearch()
    {
        using var scope = new ProjectScope();
        var invalid = await scope.Service.CreateResearchAsync("Study", ResearchProjectTypeCodes.Other, null, "F", "S", "M");
        Assert.Contains("CustomProjectTypeRequired", invalid.ValidationErrors!);
        var valid = await scope.Service.CreateResearchAsync("Study", ResearchProjectTypeCodes.Other, "Monograph", "F", "S", "M");
        await scope.Service.ArchiveAsync(valid.Project!.Id, true);
        Assert.Equal(ProjectKindCodes.Research, Assert.Single((await scope.Service.ListAsync(true)).Projects).Kind);
        await scope.Service.ArchiveAsync(valid.Project.Id, false);
        Assert.Equal(ProjectKindCodes.Research, Assert.Single((await scope.Service.ListAsync(false)).Projects).Kind);
    }

    [Fact]
    public async Task ProjectKindCannotBeChangedAfterCreation()
    {
        using var scope = new ProjectScope();
        var original = (await scope.Service.CreateAsync("Design", ProjectTypeCodes.Coursework)).Project!;
        var replacement = new ProjectRecord
        {
            Id = original.Id, Kind = ProjectKindCodes.Research, Name = original.Name,
            Type = ResearchProjectTypeCodes.Coursework,
            ResearchDetails = new() { ResearchField = "F", ResearchSubject = "S", ResearchMethods = "M" },
            CreatedAtUtc = original.CreatedAtUtc, UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        var saved = await scope.Service.SaveAsync(replacement);
        Assert.Contains("ProjectKindImmutable", saved.ValidationErrors!);
        Assert.Equal(ProjectKindCodes.Design, (await scope.Service.ReadAsync(original.Id)).Value!.Kind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PermanentDeleteRemovesOnlyOwnedDataAndClearsFolderToken(bool archived)
    {
        using var scope = new ProjectScope();
        var survivor = (await scope.Service.CreateAsync("Survivor", ProjectTypeCodes.Personal)).Project!;
        var project = (await scope.Service.CreateAsync("Delete", ProjectTypeCodes.Research)).Project!;
        var external = Path.Combine(Path.GetTempPath(), $"UrbanPlanToolbox-external-{Guid.NewGuid():N}");
        Directory.CreateDirectory(external);
        await File.WriteAllTextAsync(Path.Combine(external, "keep.txt"), "keep");
        try
        {
            project.WorkFolder = new ProjectFolderReference { AccessToken = "token", DisplayName = "External", DisplayPath = external };
            project.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await scope.Service.SaveAsync(project);
            await File.WriteAllTextAsync(Path.Combine(scope.Provider.GetProjectAttachmentsDirectory(project.Id), "inside.txt"), "delete");
            if (archived) await scope.Service.ArchiveAsync(project.Id, true);
            var folderAccess = new RecordingFolderAccess();

            var deleted = await scope.Service.DeleteAsync(project.Id, folderAccess);

            Assert.True(deleted.Succeeded);
            Assert.False(Directory.Exists(Path.Combine(scope.Provider.Paths.ProjectsDirectory, project.Id.ToString("D"))));
            Assert.False(Directory.Exists(Path.Combine(scope.Provider.Paths.ProjectBackupsDirectory, project.Id.ToString("D"))));
            Assert.False(Directory.Exists(Path.Combine(scope.Provider.Paths.ProjectAttachmentsDirectory, project.Id.ToString("D"))));
            Assert.True(File.Exists(Path.Combine(external, "keep.txt")));
            Assert.Equal("token", folderAccess.ClearedToken);
            Assert.Equal(survivor.Id, Assert.Single((await scope.Service.ListAsync(false)).Projects).Id);
        }
        finally { if (Directory.Exists(external)) Directory.Delete(external, true); }
    }

    [Fact]
    public async Task PermanentDeleteFailureAfterStagingRollsBackProjectAndIndex()
    {
        using var scope = new ProjectScope();
        var project = (await scope.Service.CreateAsync("Rollback", ProjectTypeCodes.Research)).Project!;
        await File.WriteAllTextAsync(Path.Combine(scope.Provider.GetProjectAttachmentsDirectory(project.Id), "inside.txt"), "keep");
        var failing = new ProjectStorageService(scope.Provider, deleteFailureInjector: step => step == "AfterIndex");

        var deleted = await failing.DeleteAsync(project.Id);

        Assert.Equal(DataStorageStatus.IoFailure, deleted.Status);
        Assert.True(File.Exists(scope.Provider.GetProjectDataFilePath(project.Id)));
        Assert.True(File.Exists(Path.Combine(scope.Provider.GetProjectAttachmentsDirectory(project.Id), "inside.txt")));
        Assert.Equal(project.Id, Assert.Single((await scope.Service.ListAsync(false)).Projects).Id);
    }

    [Theory]
    [InlineData("Canal Study", "Canal Study", true)]
    [InlineData("Canal Study", "  Canal Study  ", true)]
    [InlineData("Canal Study", "canal study", false)]
    [InlineData("Canal Study", "", false)]
    public void PermanentDeleteConfirmationRequiresExactProjectName(string projectName, string confirmation, bool expected) =>
        Assert.Equal(expected, ProjectValidation.MatchesDeleteConfirmation(projectName, confirmation));

    [Fact]
    public async Task ValidPlanningSnapshotKeepsExactValuesNullsAndStableId()
    {
        using var scope = new ProjectScope();
        var project = (await scope.Service.CreateAsync("Snapshot", ProjectTypeCodes.Research)).Project!;
        var input = new PlanningInput { SiteArea = 3m, AboveGroundArea = 1m };
        var result = new PlanningResult { FloorAreaRatio = 1m / 3m, GreenRatio = null };
        var saved = await scope.Service.AddSnapshotAsync(project.Id, input, result, "Baseline");
        var snapshotId = saved.Project!.PlanningSnapshots[0].Id;
        var read = (await scope.Service.ReadAsync(project.Id)).Value!.PlanningSnapshots[0];
        Assert.Equal(snapshotId, read.Id);
        Assert.Equal(1m / 3m, read.Result.FloorAreaRatio);
        Assert.Null(read.Result.GreenRatio);
        Assert.Equal("planning-indicator-v1", read.CalculationModel);
        await scope.Service.DeleteSnapshotAsync(project.Id, snapshotId);
        Assert.Empty((await scope.Service.ReadAsync(project.Id)).Value!.PlanningSnapshots);
    }

    [Fact]
    public async Task InvalidPlanningResultIsNotSaved()
    {
        using var scope = new ProjectScope();
        var project = (await scope.Service.CreateAsync("Snapshot", ProjectTypeCodes.Research)).Project!;
        var result = new PlanningResult(); result.Errors.Add("bad");
        var saved = await scope.Service.AddSnapshotAsync(project.Id, new PlanningInput(), result);
        Assert.Contains("PlanningResultInvalid", saved.ValidationErrors!);
        Assert.Empty((await scope.Service.ReadAsync(project.Id)).Value!.PlanningSnapshots);
    }

    [Fact]
    public void ProjectPathsAreSeparateFromToolPathsAndRejectEmptyIds()
    {
        using var scope = new ProjectScope();
        var id = Guid.NewGuid();
        Assert.StartsWith(scope.Provider.Paths.ProjectsDirectory, scope.Provider.GetProjectDataDirectory(id));
        Assert.StartsWith(scope.Provider.Paths.ProjectBackupsDirectory, scope.Provider.GetProjectBackupDirectory(id));
        Assert.StartsWith(scope.Provider.Paths.ProjectAttachmentsDirectory, scope.Provider.GetProjectAttachmentsDirectory(id));
        Assert.Throws<ArgumentException>(() => scope.Provider.GetProjectDataDirectory(Guid.Empty));
    }

    private static ProjectRecord SampleProject(Guid id, string name)
    {
        var now = DateTimeOffset.UtcNow;
        return new() { Id = id, Name = name, Type = ProjectTypeCodes.Research, DesignDetails = new(), CreatedAtUtc = now, UpdatedAtUtc = now };
    }

    private sealed class ProjectScope : IDisposable
    {
        public ProjectScope()
        {
            Root = Path.Combine(Path.GetTempPath(), $"UrbanPlanToolbox-projects-{Guid.NewGuid():N}");
            Provider = new AppDataPathProvider(Root, [ToolIds.PlanningIndicatorCalculator, ToolIds.UnitScaleConverter]);
            Provider.EnsureInfrastructureDirectories();
            Service = new ProjectStorageService(Provider);
        }

        public string Root { get; }
        public AppDataPathProvider Provider { get; }
        public ProjectStorageService Service { get; }
        public void Dispose() { if (Directory.Exists(Root)) Directory.Delete(Root, true); }
    }

    private sealed class RecordingFolderAccess : IProjectFolderAccessService
    {
        public string? ClearedToken { get; private set; }
        public Task<ProjectFolderAccessResult> SelectAsync(Guid projectId, ProjectFolderReference? current = null) => throw new NotSupportedException();
        public Task<ProjectFolderAccessResult> OpenAsync(ProjectFolderReference reference) => throw new NotSupportedException();
        public void Clear(ProjectFolderReference? reference) => ClearedToken = reference?.AccessToken;
    }
}
