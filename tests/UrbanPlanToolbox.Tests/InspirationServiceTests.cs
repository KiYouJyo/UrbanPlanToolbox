using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Services;
using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class InspirationServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "UrbanPlanToolbox-inspiration-" + Guid.NewGuid().ToString("N"));
    private InspirationService Create() => new(Path.Combine(_root, "inspirations.json"));
    [Fact]
    public async Task EmptyOrDirtyDraftNeverEntersSavedCollection()
    {
        var service = Create(); await service.SaveDraftAsync(new InspirationDraft()); Assert.Empty(await service.ListAsync());
        await service.SaveDraftAsync(new InspirationDraft { Content = "idea" }); Assert.Null(await service.SaveDraftAsInspirationAsync()); Assert.Empty(await service.ListAsync());
    }
    [Fact]
    public async Task ValidDraftConvertsOnceAndPreservesOptionalContent()
    {
        var service = Create(); await service.SaveDraftAsync(new InspirationDraft { Title = "  A title  ", Category = InspirationCategory.Research });
        var item = await service.SaveDraftAsInspirationAsync(); Assert.NotNull(item); Assert.Equal("A title", item!.Title); Assert.Equal(InspirationCategory.Research, item.Category); Assert.Equal(string.Empty, item.Content); Assert.Null(await service.GetDraftAsync()); Assert.Single(await service.ListAsync());
    }
    [Fact]
    public async Task SavedInspirationUpdatesAndDeletesWithoutDuplicates()
    {
        var service = Create(); await service.SaveDraftAsync(new InspirationDraft { Title = "A" }); var item = (await service.SaveDraftAsInspirationAsync())!;
        item.Title = "B"; item.LinkedProjectId = Guid.NewGuid(); Assert.True(await service.SaveAsync(item)); var saved = Assert.Single(await service.ListAsync()); Assert.Equal("B", saved.Title); Assert.NotEqual(saved.CreatedAt, saved.UpdatedAt); Assert.True(await service.DeleteAsync(item.Id)); Assert.Empty(await service.ListAsync());
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
