using Windows.Graphics;

namespace UrbanPlanToolbox.Services;

/// <summary>Persists only the shell's last usable size and maximize preference.</summary>
public sealed class WindowPlacementService
{
    public const int MinimumWidth = 320;
    public const int MinimumHeight = 240;
    private const int MaximumSavedDimension = 16384;
    private readonly SettingsService _settings;

    public WindowPlacementService(SettingsService? settings = null) => _settings = settings ?? new SettingsService();

    public WindowPlacement Load(SizeInt32 workArea)
    {
        var settings = _settings.Load();
        var placement = IsValidSavedSize(settings.LastNormalWindowWidth, settings.LastNormalWindowHeight)
            ? new WindowPlacement(settings.LastNormalWindowWidth!.Value, settings.LastNormalWindowHeight!.Value, settings.WasWindowMaximized)
            : CreateDefault(workArea);
        return ClampToWorkArea(placement, workArea);
    }

    /// <summary>First launch uses a medium, work-area-proportional size so a high-DPI desktop never opens a tiny window.</summary>
    public static WindowPlacement CreateDefault(SizeInt32 workArea)
    {
        var width = Math.Max(MinimumWidth, (int)Math.Round(workArea.Width * 0.70));
        var height = Math.Max(MinimumHeight, (int)Math.Round(workArea.Height * 0.75));
        return new WindowPlacement(width, height, false);
    }

    public void Save(SizeInt32 lastNormalSize, bool wasMaximized)
    {
        if (!IsValidSavedSize(lastNormalSize.Width, lastNormalSize.Height)) return;
        _settings.Update(settings =>
        {
            settings.LastNormalWindowWidth = lastNormalSize.Width;
            settings.LastNormalWindowHeight = lastNormalSize.Height;
            settings.WasWindowMaximized = wasMaximized;
        });
    }

    public static WindowPlacement ClampToWorkArea(WindowPlacement placement, SizeInt32 workArea)
    {
        var maxWidth = Math.Max(1, workArea.Width);
        var maxHeight = Math.Max(1, workArea.Height);
        return placement with
        {
            Width = Math.Clamp(placement.Width, Math.Min(MinimumWidth, maxWidth), maxWidth),
            Height = Math.Clamp(placement.Height, Math.Min(MinimumHeight, maxHeight), maxHeight)
        };
    }

    private static bool IsValidSavedSize(int? width, int? height) =>
        width is >= MinimumWidth and <= MaximumSavedDimension &&
        height is >= MinimumHeight and <= MaximumSavedDimension;
}

public sealed record WindowPlacement(int Width, int Height, bool WasMaximized);
