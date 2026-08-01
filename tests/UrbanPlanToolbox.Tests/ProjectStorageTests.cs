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
        var future = new DataEnvelope<object> { SchemaVersion = 2, SavedAtUtc = DateTimeOffset.UtcNow, Payload = new { id, name = "Future" } };
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
        return new() { Id = id, Name = name, Type = ProjectTypeCodes.Research, CreatedAtUtc = now, UpdatedAtUtc = now };
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
}
