using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
        var localization = LocalizationService.Default;
        CategoryList.ItemsSource = categories
            .Select(category => new CategoryDisplayItem(
                category.Id,
                localization.GetString(category.NameResourceKey),
                category))
            .ToArray();

        var selectedCategory = SessionSelections.TryGetValue(primaryCategory, out var savedCategory)
            ? savedCategory
            : defaultCategory;
        var items = CategoryList.Items.OfType<CategoryDisplayItem>().ToArray();
        CategoryList.SelectedItem = items.FirstOrDefault(item => item.Definition.SecondaryCategory == selectedCategory)
            ?? items.FirstOrDefault();
    }

    private void OnCategorySelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CategoryList.SelectedItem is not CategoryDisplayItem { Definition: { } category })
        {
            return;
        }

        SessionSelections[_primaryCategory] = category.SecondaryCategory;
        var localization = LocalizationService.Default;
        var tools = ToolRegistry.Default
            .GetAvailableByCategories(_primaryCategory, category.SecondaryCategory)
            .Select(tool => new LocalizedTool(
                tool,
                localization.GetString(tool.NameResourceKey),
                localization.GetString(tool.DescriptionResourceKey)))
            .ToArray();
        ToolCards.SetTools(tools);
        ToolCards.Visibility = tools.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        EmptyState.IsOpen = tools.Length == 0;
    }

    private sealed record CategoryDisplayItem(string Id, string DisplayName, ToolCategoryDefinition Definition);
}
