using System.Text.Json;
using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.Services;

/// <summary>Small, isolated local store. Drafts deliberately have no identity and are not in Items.</summary>
public sealed class InspirationService
{
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);
    public static InspirationService Default { get; } = new();
    public InspirationService(string? path = null) => _path = path ?? Path.Combine(AppDataPathProvider.Default.Paths.DataDirectory, "inspirations.json");

    public async Task<IReadOnlyList<Inspiration>> ListAsync(CancellationToken token = default)
    {
        var document = await ReadAsync(token); return document.Items.OrderBy(x => x.CreatedAt).ToArray();
    }
    public async Task<InspirationDraft?> GetDraftAsync(CancellationToken token = default) => (await ReadAsync(token)).Draft;
    public async Task SaveDraftAsync(InspirationDraft draft, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(draft); var document = await ReadAsync(token);
        document.Draft = new InspirationDraft { Category = draft.Category, Title = draft.Title ?? string.Empty, Content = draft.Content ?? string.Empty };
        await WriteAsync(document, token);
    }
    public async Task<Inspiration?> SaveDraftAsInspirationAsync(CancellationToken token = default)
    {
        var document = await ReadAsync(token); var draft = document.Draft;
        if (draft is null || string.IsNullOrWhiteSpace(draft.Title)) return null;
        var now = DateTimeOffset.UtcNow;
        var item = new Inspiration { Id = Guid.NewGuid(), Category = draft.Category, Title = draft.Title.Trim(), Content = draft.Content?.Trim() ?? string.Empty, CreatedAt = now, UpdatedAt = now };
        document.Items.Add(item); document.Draft = null; await WriteAsync(document, token); return item;
    }
    public async Task<bool> CreateAsync(Inspiration item, CancellationToken token = default)
    {
        if (item.Id == Guid.Empty || string.IsNullOrWhiteSpace(item.Title)) return false;
        var document = await ReadAsync(token);
        if (document.Items.Any(existing => existing.Id == item.Id)) return false;
        var now = DateTimeOffset.UtcNow;
        document.Items.Add(new Inspiration { Id = item.Id, Category = item.Category, Title = item.Title.Trim(), Content = item.Content?.Trim() ?? string.Empty, LinkedProjectId = item.LinkedProjectId, CreatedAt = item.CreatedAt == default ? now : item.CreatedAt, UpdatedAt = now });
        await WriteAsync(document, token); return true;
    }
    public async Task<bool> SaveAsync(Inspiration item, CancellationToken token = default)
    {
        if (item.Id == Guid.Empty || string.IsNullOrWhiteSpace(item.Title)) return false;
        var document = await ReadAsync(token); var existing = document.Items.FirstOrDefault(x => x.Id == item.Id); if (existing is null) return false;
        existing.Title = item.Title.Trim(); existing.Content = item.Content?.Trim() ?? string.Empty; existing.Category = item.Category; existing.LinkedProjectId = item.LinkedProjectId; existing.UpdatedAt = DateTimeOffset.UtcNow;
        await WriteAsync(document, token); return true;
    }
    public async Task<bool> DeleteAsync(Guid id, CancellationToken token = default)
    {
        var document = await ReadAsync(token); if (document.Items.RemoveAll(x => x.Id == id) == 0) return false; await WriteAsync(document, token); return true;
    }
    private async Task<InspirationDocument> ReadAsync(CancellationToken token)
    {
        await _gate.WaitAsync(token); try { if (!File.Exists(_path)) return new(); var json = await File.ReadAllTextAsync(_path, token); return JsonSerializer.Deserialize<InspirationDocument>(json) ?? new(); }
        catch (JsonException) { return new(); } finally { _gate.Release(); }
    }
    private async Task WriteAsync(InspirationDocument document, CancellationToken token)
    {
        await _gate.WaitAsync(token); try { Directory.CreateDirectory(Path.GetDirectoryName(_path)!); var temp = _path + ".tmp"; await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(document), token); File.Move(temp, _path, true); }
        finally { _gate.Release(); }
    }
}
