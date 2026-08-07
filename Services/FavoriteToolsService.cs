using UrbanPlanToolbox.Models.Tools;

namespace UrbanPlanToolbox.Services;

public sealed class FavoriteToolsService
{
    private readonly SettingsService _settingsService;
    private readonly ToolRegistry _toolRegistry;
    private readonly HashSet<string> _favoriteIds;

    public static FavoriteToolsService Default { get; } = new(new SettingsService(), ToolRegistry.Default);

    public FavoriteToolsService(SettingsService settingsService, ToolRegistry toolRegistry)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _favoriteIds = new HashSet<string>(StringComparer.Ordinal);

        var storedIds = _settingsService.Load().FavoriteToolIds ?? [];
        foreach (var id in storedIds)
        {
            if (IsAvailableTool(id))
            {
                _favoriteIds.Add(id);
            }
        }
    }

    public event EventHandler? FavoritesChanged;

    public bool IsFavorite(string? toolId) => toolId is not null && _favoriteIds.Contains(toolId);

    public bool Add(string? toolId)
    {
        if (!IsAvailableTool(toolId) || !_favoriteIds.Add(toolId!))
        {
            return false;
        }

        PersistAndNotify();
        return true;
    }

    public bool Remove(string? toolId)
    {
        if (toolId is null || !_favoriteIds.Remove(toolId))
        {
            return false;
        }

        PersistAndNotify();
        return true;
    }

    public bool Toggle(string? toolId)
    {
        if (!IsAvailableTool(toolId))
        {
            return false;
        }

        if (_favoriteIds.Contains(toolId!))
        {
            Remove(toolId);
            return false;
        }

        Add(toolId);
        return true;
    }

    public IReadOnlyList<ToolDefinition> GetFavoriteTools() =>
        _toolRegistry.All
            .Where(tool => tool.IsAvailable && tool.Visibility == ToolVisibility.Visible && tool.SupportsFavorites && _favoriteIds.Contains(tool.Id))
            .ToArray();

    private bool IsAvailableTool(string? toolId) =>
        _toolRegistry.TryGet(toolId, out var tool) && tool is { IsAvailable: true, Visibility: ToolVisibility.Visible, SupportsFavorites: true };

    private void PersistAndNotify()
    {
        var orderedIds = GetFavoriteTools().Select(tool => tool.Id).ToList();
        _settingsService.Update(settings => settings.FavoriteToolIds = orderedIds);
        FavoritesChanged?.Invoke(this, EventArgs.Empty);
    }
}
