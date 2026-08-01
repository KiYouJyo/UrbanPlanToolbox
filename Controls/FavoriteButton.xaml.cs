using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using UrbanPlanToolbox.Services;

namespace UrbanPlanToolbox.Controls;

public sealed partial class FavoriteButton : UserControl
{
    private readonly ILocalizationService _localization = LocalizationService.Default;

    public static readonly DependencyProperty ToolIdProperty = DependencyProperty.Register(
        nameof(ToolId),
        typeof(string),
        typeof(FavoriteButton),
        new PropertyMetadata(null, OnToolIdChanged));

    public FavoriteButton()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public string? ToolId
    {
        get => (string?)GetValue(ToolIdProperty);
        set => SetValue(ToolIdProperty, value);
    }

    private static void OnToolIdChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args) =>
        ((FavoriteButton)dependencyObject).Refresh();

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        FavoriteToolsService.Default.FavoritesChanged += OnFavoritesChanged;
        Refresh();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) =>
        FavoriteToolsService.Default.FavoritesChanged -= OnFavoritesChanged;

    private void OnFavoritesChanged(object? sender, EventArgs e) => Refresh();

    private void OnClick(object sender, RoutedEventArgs e) =>
        FavoriteToolsService.Default.Toggle(ToolId);

    private void Refresh()
    {
        if (Toggle is null || FavoriteIcon is null)
        {
            return;
        }

        var isFavorite = FavoriteToolsService.Default.IsFavorite(ToolId);
        Toggle.IsChecked = isFavorite;
        FavoriteIcon.Glyph = isFavorite ? "\uE735" : "\uE734";
        var label = isFavorite
            ? _localization.GetString("Action_RemoveFavorite")
            : _localization.GetString("Action_AddFavorite");
        ToolTipService.SetToolTip(Toggle, label);
        AutomationProperties.SetName(Toggle, label);
    }
}
