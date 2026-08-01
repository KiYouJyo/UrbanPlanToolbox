using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UrbanPlanToolbox.Services;

namespace UrbanPlanToolbox.Views;

public sealed partial class CommonToolsPage : Page
{
    private readonly ToolSearchService _toolSearchService = new(ToolRegistry.Default);

    public CommonToolsPage()
    {
        InitializeComponent();
        NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        FavoriteToolsService.Default.FavoritesChanged += OnFavoritesChanged;
        RefreshTools();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) =>
        FavoriteToolsService.Default.FavoritesChanged -= OnFavoritesChanged;

    private void OnFavoritesChanged(object? sender, EventArgs e) => RefreshTools();

    private void OnSearchTextChanged(object sender, TextChangedEventArgs args) => RefreshTools();

    private void OnClearSearch(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = string.Empty;
        SearchBox.Focus(FocusState.Programmatic);
    }

    private void RefreshTools()
    {
        var groups = _toolSearchService.Search(SearchBox.Text, tool => FavoriteToolsService.Default.IsFavorite(tool.Id));
        ToolList.SetGroups(groups);
        ToolList.Visibility = groups.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        EmptyState.IsOpen = groups.Count == 0;
    }
}
