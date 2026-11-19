using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace UrbanPlanToolbox.Services;

/// <summary>Enforces Acrylic for all app windows, including previously configured Mica installations.</summary>
public static class WindowBackdropPreference
{
    public static void Apply(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (window.SystemBackdrop is not DesktopAcrylicBackdrop)
            window.SystemBackdrop = new DesktopAcrylicBackdrop();
    }
}
