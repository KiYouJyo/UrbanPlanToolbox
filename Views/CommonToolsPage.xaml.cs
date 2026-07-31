using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UrbanPlanToolbox.Services;

namespace UrbanPlanToolbox.Views;

public sealed partial class CommonToolsPage : Page
{
    public CommonToolsPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        FavoriteToolsService.Default.FavoritesChanged += OnFavoritesChanged;
        RefreshFavorites();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) =>
        FavoriteToolsService.Default.FavoritesChanged -= OnFavoritesChanged;

    private void OnFavoritesChanged(object? sender, EventArgs e) => RefreshFavorites();

    private void RefreshFavorites()
    {
        var tools = FavoriteToolsService.Default.GetFavoriteTools();
        FavoriteCards.SetTools(tools);
        FavoriteCards.Visibility = tools.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        EmptyState.IsOpen = tools.Count == 0;
    }
}
