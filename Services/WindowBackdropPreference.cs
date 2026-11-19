using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace UrbanPlanToolbox.Services;

/// <summary>Changes the native window backdrop without rebuilding its content.</summary>
public static class WindowBackdropPreference
{
    public static void Apply(Window window, string? material)
    {
        ArgumentNullException.ThrowIfNull(window);
        switch (SettingsService.NormalizeBackdropMaterial(material))
        {
            case AppBackdropMaterial.Acrylic:
                if (window.SystemBackdrop is not DesktopAcrylicBackdrop)
                    window.SystemBackdrop = new DesktopAcrylicBackdrop();
                break;
            default:
                if (window.SystemBackdrop is not MicaBackdrop)
                    window.SystemBackdrop = new MicaBackdrop();
                break;
        }
    }
}
