using System.Text.Json;
using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.Services;

/// <summary>
/// Local inspiration document. A named OS semaphore guards the entire read/modify/write
/// transaction because the WinUI app and Widgets Board COM server run in separate processes.
/// Drafts remain separate from saved Items.
/// </summary>
public sealed class InspirationService
{
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private static readonly Semaphore? CrossProcessGate = OperatingSystem.IsWindows()
        ? new Semaphore(1, 1, @"Local\UrbanPlanToolbox.InspirationData.v1")
        : null;

    public static InspirationService Default { get; } = new();

    public InspirationService(string? path = null) =>
        _path = path ?? Path.Combine(AppDataPathProvider.Default.Paths.DataDirectory, "inspirations.json");

    public Task<IReadOnlyList<Inspiration>> ListAsync(CancellationToken token = default) =>
        AccessAsync(document =>
            ((IReadOnlyList<Inspiration>)document.Items.OrderBy(x => x.CreatedAt).ToArray(), false), token);

    public Task<InspirationDraft?> GetDraftAsync(CancellationToken token = default) =>
        AccessAsync(document => (document.Draft, false), token);

    public Task SaveDraftAsync(InspirationDraft draft, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        return AccessAsync(document =>
        {
            document.Draft = new InspirationDraft
            {
                Category = draft.Category,
                Title = draft.Title ?? string.Empty,
                Content = draft.Content ?? string.Empty
            };
            return (true, true);
        }, token);
    }

    public Task<Inspiration?> SaveDraftAsInspirationAsync(CancellationToken token = default) =>
        AccessAsync(document =>
        {
            var draft = document.Draft;
            if (draft is null || string.IsNullOrWhiteSpace(draft.Title))
                return ((Inspiration?)null, false);
            var now = DateTimeOffset.UtcNow;
            var item = new Inspiration
            {
                Id = Guid.NewGuid(),
                Category = draft.Category,
                Title = draft.Title.Trim(),
                Content = draft.Content?.Trim() ?? string.Empty,
                CreatedAt = now,
                UpdatedAt = now
            };
            document.Items.Add(item);
            document.Draft = null;
            return (item, true);
        }, token);

    public Task<bool> CreateAsync(Inspiration item, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Id == Guid.Empty || string.IsNullOrWhiteSpace(item.Title)) return Task.FromResult(false);
        return AccessAsync(document =>
        {
            if (document.Items.Any(existing => existing.Id == item.Id)) return (false, false);
            var now = DateTimeOffset.UtcNow;
            document.Items.Add(new Inspiration
            {
                Id = item.Id,
                Category = item.Category,
                Title = item.Title.Trim(),
                Content = item.Content?.Trim() ?? string.Empty,
                LinkedProjectId = item.LinkedProjectId,
                CreatedAt = item.CreatedAt == default ? now : item.CreatedAt,
                UpdatedAt = now
            });
            return (true, true);
        }, token);
    }

    public Task<bool> SaveAsync(Inspiration item, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Id == Guid.Empty || string.IsNullOrWhiteSpace(item.Title)) return Task.FromResult(false);
        return AccessAsync(document =>
        {
            var existing = document.Items.FirstOrDefault(x => x.Id == item.Id);
            if (existing is null) return (false, false);
            existing.Title = item.Title.Trim();
            existing.Content = item.Content?.Trim() ?? string.Empty;
            existing.Category = item.Category;
            existing.LinkedProjectId = item.LinkedProjectId;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            return (true, true);
        }, token);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken token = default) =>
        AccessAsync(document =>
            document.Items.RemoveAll(x => x.Id == id) > 0 ? (true, true) : (false, false), token);

    private async Task<T> AccessAsync<T>(
        Func<InspirationDocument, (T result, bool persist)> action,
        CancellationToken token)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        bool acquired = false;
        try
        {
            // Semaphore (not Mutex) can be released after an await on another thread.
            // Only hold it during short local-file transactions; the provider performs no network I/O.
            token.ThrowIfCancellationRequested();
            if (CrossProcessGate is not null)
            {
                while (!CrossProcessGate.WaitOne(100))
                    token.ThrowIfCancellationRequested();
                acquired = true;
            }
            var document = await ReadCoreAsync(token).ConfigureAwait(false);
            var (result, persist) = action(document);
            if (persist) await WriteCoreAsync(document, token).ConfigureAwait(false);
            return result;
        }
        finally
        {
            if (acquired) CrossProcessGate!.Release();
            _gate.Release();
        }
    }

    private async Task<InspirationDocument> ReadCoreAsync(CancellationToken token)
    {
        try
        {
            if (!File.Exists(_path)) return new();
            var json = await File.ReadAllTextAsync(_path, token).ConfigureAwait(false);
            return JsonSerializer.Deserialize<InspirationDocument>(json) ?? new();
        }
        catch (JsonException) { return new(); }
    }

    private async Task WriteCoreAsync(InspirationDocument document, CancellationToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temp = _path + ".tmp";
        await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(document), token).ConfigureAwait(false);
        File.Move(temp, _path, true);
    }
}
