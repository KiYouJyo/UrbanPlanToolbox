using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using UrbanPlanToolbox.Models.Tools;
using UrbanPlanToolbox.Services;

namespace UrbanPlanToolbox.Controls;

public sealed partial class ToolCategoryBrowser : UserControl
{
    private static readonly Dictionary<ToolPrimaryCategory, ToolSecondaryCategory> SessionSelections = [];
    private ToolPrimaryCategory _primaryCategory;

    public ToolCategoryBrowser() => InitializeComponent();

    public void Configure(
        ToolPrimaryCategory primaryCategory,
        IReadOnlyList<ToolCategoryDefinition> categories,
        ToolSecondaryCategory defaultCategory)
    {
        _primaryCategory = primaryCategory;
        CategoryList.ItemsSource = categories;

        var selectedCategory = SessionSelections.TryGetValue(primaryCategory, out var savedCategory)
            ? savedCategory
            : defaultCategory;
        CategoryList.SelectedItem = categories.FirstOrDefault(category => category.SecondaryCategory == selectedCategory)
            ?? categories.FirstOrDefault();
    }

    private void OnCategorySelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CategoryList.SelectedItem is not ToolCategoryDefinition category)
        {
            return;
        }

        SessionSelections[_primaryCategory] = category.SecondaryCategory;
        var tools = ToolRegistry.Default.GetAvailableByCategories(_primaryCategory, category.SecondaryCategory);
        ToolCards.ItemsSource = tools;
        ToolCards.Visibility = tools.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        EmptyState.IsOpen = tools.Count == 0;
    }

    private void OnToolCardClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ToolDefinition tool })
        {
            var frame = FindHostFrame();
            if (frame is not null)
            {
                ToolNavigation.Navigate(frame, tool.Id);
            }
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
