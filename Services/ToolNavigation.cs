using Microsoft.UI.Xaml.Controls;

namespace UrbanPlanToolbox.Services;

public static class ToolNavigation
{
    public static bool Navigate(Frame frame, string? toolId)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (!ToolRegistry.Default.TryGet(toolId, out var tool) || tool is null || !tool.IsAvailable)
        {
            return false;
        }

        if (frame.CurrentSourcePageType != tool.PageType)
        {
            frame.Navigate(tool.PageType);
        }

        return true;
    }
}
