using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using UrbanPlanToolbox.Models.Tools;
using UrbanPlanToolbox.Services;

namespace UrbanPlanToolbox.Controls;

public sealed partial class ToolCardsView : UserControl
{
    public ToolCardsView() => InitializeComponent();

    public void SetTools(IReadOnlyList<LocalizedTool> tools) => CardsRepeater.ItemsSource = tools;

    private void OnToolCardClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: LocalizedTool { Definition: { } tool } } && FindHostFrame() is { } frame)
        {
            ToolNavigation.Navigate(frame, tool.Id);
        }
    }

    private Frame? FindHostFrame()
    {
        DependencyObject? current = this;
        while (current is not null)
        {
            if (current is Frame frame)
            {
                return frame;
            }

            if (current is Page page && page.Frame is not null)
            {
                return page.Frame;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
