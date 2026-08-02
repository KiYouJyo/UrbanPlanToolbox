using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Pickers;
using Windows.UI;
using WinRT.Interop;
using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Models.Tools;
using UrbanPlanToolbox.Services;

namespace UrbanPlanToolbox.Views;

public sealed partial class ColorPaletteRecorderPage : Page
{
    private readonly ColorPaletteStorageService _storage = new(AppDataPathProvider.Default);
    private readonly ILocalizationService _localization = LocalizationService.Default;
    private ColorPaletteDocument _document = new();
    private ColorPaletteScheme? _editing;
    private bool _isNew;
    private readonly List<ColorPaletteImage> _pendingImageDeletes = [];
    private readonly List<ColorPaletteImage> _newImages = [];
    private bool _dirty;

    public ColorPaletteRecorderPage()
    {
        InitializeComponent();
        TitleText.Text = T("Tool_ColorPaletteRecorder_Name"); DescriptionText.Text = T("Tool_ColorPaletteRecorder_Description");
        NewButton.Content = T("Palette_New"); BackButton.Content = T("Action_Back"); AddImagesButton.Content = T("Palette_AddImages"); AddColorButton.Content = T("Palette_AddColor"); SaveButton.Content = T("Palette_Save"); ResetButton.Content = T("Palette_Reset");
        ImagesTitle.Text = T("Palette_Images"); ColorsTitle.Text = T("Palette_Colors");
        CategoryFilter.Items.Add(new Choice("", T("Palette_AllCategories")));
        CategoryBox.Items.Add(new Choice(ColorPaletteCategories.Warm, T("Palette_CategoryWarm"))); CategoryBox.Items.Add(new Choice(ColorPaletteCategories.Cool, T("Palette_CategoryCool"))); CategoryBox.Items.Add(new Choice(ColorPaletteCategories.Neutral, T("Palette_CategoryNeutral"))); CategoryBox.Items.Add(new Choice(ColorPaletteCategories.Monochrome, T("Palette_CategoryMonochrome"))); CategoryBox.Items.Add(new Choice(ColorPaletteCategories.Mixed, T("Palette_CategoryMixed"))); CategoryBox.Items.Add(new Choice(ColorPaletteCategories.Custom, T("Palette_CategoryCustom")));
        foreach (var option in CategoryBox.Items.OfType<Choice>()) CategoryFilter.Items.Add(option);
        CategoryFilter.SelectedIndex = 0; FavoriteButtonPlaceholder(); Loaded += async (_, _) => await LoadAsync();
    }

    private void FavoriteButtonPlaceholder() { /* Tool cards own the favorite action; this page is intentionally scheme-focused. */ }
    private string T(string key) => _localization.GetString(key);
    private string T(string key, params object[] values) => string.Format(_localization.GetString(key), values);

    private async Task LoadAsync()
    {
        var result = await _storage.ReadAsync();
        if (!result.HasValue) { Show(ListStatus, T("Palette_LoadFailed")); return; }
        _document = result.Value!; RenderCards();
    }

    private void RenderCards()
    {
        CardsPanel.Children.Clear();
        var category = (CategoryFilter.SelectedItem as Choice)?.Id;
        var schemes = _document.Schemes.Where(s => string.IsNullOrEmpty(category) || s.Category == category).OrderByDescending(s => s.UpdatedAtUtc).ToArray();
        if (schemes.Length == 0) { CardsPanel.Children.Add(new TextBlock { Text = T("Palette_Empty"), TextWrapping = TextWrapping.Wrap }); return; }
        foreach (var scheme in schemes)
        {
            var card = new Border { Padding = new Thickness(14), CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1), BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"] };
            var row = new Grid(); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var preview = CreateCardPreview(scheme);
            Grid.SetColumn(preview, 0); row.Children.Add(preview);
            var details = new StackPanel { Spacing = 3 }; details.Children.Add(new TextBlock { Text = scheme.Name, Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"], TextWrapping = TextWrapping.Wrap }); details.Children.Add(new TextBlock { Text = $"{CategoryName(scheme)} · {T("Palette_ImageCount", scheme.Images.Count)} · {T("Palette_ColorCount", scheme.Colors.Count)}", TextWrapping = TextWrapping.Wrap }); Grid.SetColumn(details, 1); row.Children.Add(details);
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 }; var edit = new Button { Content = T("Palette_Edit"), Tag = scheme }; edit.Click += (sender, _) => OpenEditor((ColorPaletteScheme)((Button)sender).Tag); var delete = new Button { Content = T("Action_Delete"), Tag = scheme }; delete.Click += async (sender, _) => await DeleteAsync((ColorPaletteScheme)((Button)sender).Tag); actions.Children.Add(edit); actions.Children.Add(delete); Grid.SetColumn(actions, 2); row.Children.Add(actions);
            card.Child = row; card.Tapped += (_, _) => OpenEditor(scheme); CardsPanel.Children.Add(card);
        }
    }

    private void OnFilterChanged(object sender, SelectionChangedEventArgs e) => RenderCards();
    private void OnNew(object sender, RoutedEventArgs e) { OpenEditor(new ColorPaletteScheme { Name = T("Palette_NewScheme") }, true); }
    private void OpenEditor(ColorPaletteScheme scheme, bool isNew = false)
    {
        _editing = ColorPaletteStorageService.CloneScheme(scheme); _isNew = isNew; _pendingImageDeletes.Clear(); _newImages.Clear(); _dirty = false; EditorStatus.IsOpen = false; ListPanel.Visibility = Visibility.Collapsed; EditorPanel.Visibility = Visibility.Visible; EditorTitle.Text = scheme.Name; SchemeNameBox.Header = T("Palette_Name"); SchemeNameBox.Text = scheme.Name; CustomCategoryBox.Header = T("Palette_CustomCategory");
        CategoryBox.SelectedItem = CategoryBox.Items.OfType<Choice>().First(x => x.Id == scheme.Category); CustomCategoryBox.Text = scheme.CustomCategoryName ?? ""; RenderImages(); RenderColors();
    }
    private async void OnBack(object sender, RoutedEventArgs e)
    {
        if (_dirty && !await ConfirmDiscardAsync()) return;
        DiscardDraft(); _editing = null; _dirty = false; EditorPanel.Visibility = Visibility.Collapsed; ListPanel.Visibility = Visibility.Visible; RenderCards();
    }
    private void OnChanged(object sender, TextChangedEventArgs e) { if (_editing is null) return; _editing.Name = SchemeNameBox.Text.Trim(); _editing.CustomCategoryName = CustomCategoryBox.Text.Trim(); _dirty = true; }
    private void OnCategoryChanged(object sender, SelectionChangedEventArgs e) { if (_editing is null || CategoryBox.SelectedItem is not Choice choice) return; _editing.Category = choice.Id; CustomCategoryBox.Visibility = choice.Id == ColorPaletteCategories.Custom ? Visibility.Visible : Visibility.Collapsed; _dirty = true; }
    private async void OnAddImages(object sender, RoutedEventArgs e)
    {
        if (_editing is null || App.MainWindow is null) return;
        var picker = new FileOpenPicker(); picker.FileTypeFilter.Add(".png"); picker.FileTypeFilter.Add(".jpg"); picker.FileTypeFilter.Add(".jpeg"); picker.FileTypeFilter.Add(".webp"); picker.FileTypeFilter.Add(".bmp"); InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));
        var files = await picker.PickMultipleFilesAsync(); foreach (var file in files) { try { var image = await _storage.CopyImageAsync(_editing.SchemeId, file.Path, _editing.Images.Count); _editing.Images.Add(image); _newImages.Add(image); } catch { Show(EditorStatus, T("Palette_ImageImportFailed")); } }
        _dirty = true; RenderImages();
    }
    private void RenderImages()
    {
        ImagesList.Items.Clear(); if (_editing is null) return;
        foreach (var image in _editing.Images.OrderBy(x => x.SortOrder))
        {
            var panel = new StackPanel { Width = 180, Margin = new Thickness(0, 0, 8, 8), Spacing = 4 }; var path = _storage.ResolveManagedImagePath(image.RelativePath);
            if (File.Exists(path)) panel.Children.Add(new Image { Source = new BitmapImage(new Uri(path)), Height = 120, Stretch = Microsoft.UI.Xaml.Media.Stretch.UniformToFill }); else panel.Children.Add(new TextBlock { Text = T("Palette_ImageMissing"), Height = 120, TextWrapping = TextWrapping.Wrap });
            var remove = new Button { Content = T("Action_Delete"), Tag = image }; remove.Click += (sender, _) => { var item = (ColorPaletteImage)((Button)sender).Tag; if (!_newImages.Remove(item)) _pendingImageDeletes.Add(item); _editing.Images.Remove(item); _dirty = true; RenderImages(); }; panel.Children.Add(remove); ImagesList.Items.Add(panel);
        }
    }
    private void OnAddColor(object sender, RoutedEventArgs e) { if (_editing is null) return; _editing.Colors.Add(new ColorPaletteColor { Hex = "#000000", SortOrder = _editing.Colors.Count }); _dirty = true; RenderColors(); }
    private void RenderColors()
    {
        ColorsPanel.Children.Clear(); if (_editing is null) return;
        foreach (var color in _editing.Colors.OrderBy(x => x.SortOrder))
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 }; var swatch = new Border { Width = 32, Height = 32, Background = new SolidColorBrush(ParseColor(color.Hex)) }; var picker = new ColorPicker { Color = ParseColor(color.Hex), Width = 180 }; var name = new TextBox { Text = color.Name ?? "", PlaceholderText = T("Palette_ColorName"), Width = 180 }; var hex = new TextBox { Text = color.Hex, Width = 120 }; var error = new TextBlock { Foreground = new SolidColorBrush(Colors.IndianRed), VerticalAlignment = VerticalAlignment.Center };
            name.TextChanged += (_, _) => { color.Name = name.Text; _dirty = true; }; hex.TextChanged += (_, _) => { if (ColorPaletteStorageService.TryNormalizeHex(hex.Text, out var value)) { color.Hex = value; swatch.Background = new SolidColorBrush(ParseColor(value)); error.Text = ""; _dirty = true; } else error.Text = T("Palette_InvalidHex"); };
            picker.ColorChanged += (_, args) => { var value = $"#{args.NewColor.R:X2}{args.NewColor.G:X2}{args.NewColor.B:X2}"; color.Hex = value; hex.Text = value; swatch.Background = new SolidColorBrush(args.NewColor); error.Text = ""; _dirty = true; };
            var remove = new Button { Content = T("Action_Delete") }; remove.Click += (_, _) => { _editing.Colors.Remove(color); _dirty = true; RenderColors(); }; row.Children.Add(swatch); row.Children.Add(picker); row.Children.Add(name); row.Children.Add(hex); row.Children.Add(error); row.Children.Add(remove); ColorsPanel.Children.Add(row);
        }
    }
    private async void OnSave(object sender, RoutedEventArgs e) { if (_editing is null) return; _editing.Name = SchemeNameBox.Text.Trim(); _editing.UpdatedAtUtc = DateTimeOffset.UtcNow; foreach (var color in _editing.Colors) if (!ColorPaletteStorageService.TryNormalizeHex(color.Hex, out var hex)) { Show(EditorStatus, T("Palette_InvalidHex")); return; } else color.Hex = hex; var next = ColorPaletteStorageService.CloneDocument(_document); var index = next.Schemes.FindIndex(s => s.SchemeId == _editing.SchemeId); if (index < 0) next.Schemes.Add(ColorPaletteStorageService.CloneScheme(_editing)); else next.Schemes[index] = ColorPaletteStorageService.CloneScheme(_editing); var result = await _storage.SaveAsync(next); if (!result.Succeeded) { Show(EditorStatus, T("Palette_SaveFailed")); return; } foreach (var image in _pendingImageDeletes) _storage.DeleteManagedImage(image); _pendingImageDeletes.Clear(); _newImages.Clear(); _document = next; _isNew = false; _dirty = false; Show(EditorStatus, T("Palette_Saved")); RenderCards(); }
    private void OnReset(object sender, RoutedEventArgs e) { if (_editing is null) return; DiscardDraft(); var saved = _document.Schemes.FirstOrDefault(s => s.SchemeId == _editing.SchemeId); if (saved is null) OpenEditor(new ColorPaletteScheme { SchemeId = _editing.SchemeId, Name = T("Palette_NewScheme") }, true); else OpenEditor(saved); }
    private async Task DeleteAsync(ColorPaletteScheme scheme) { var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = T("Palette_DeleteTitle"), Content = T("Palette_DeleteMessage"), PrimaryButtonText = T("Action_Delete"), CloseButtonText = T("Action_Cancel") }; if (await AppDialogService.Default.ShowAsync(dialog) != ContentDialogResult.Primary) return; var next = ColorPaletteStorageService.CloneDocument(_document); next.Schemes.RemoveAll(s => s.SchemeId == scheme.SchemeId); var result = await _storage.SaveAsync(next); if (!result.Succeeded) { Show(ListStatus, T("Palette_SaveFailed")); return; } _storage.DeleteSchemeAttachments(scheme.SchemeId); _document = next; RenderCards(); }
    private async Task<bool> ConfirmDiscardAsync() { var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = T("Palette_UnsavedTitle"), Content = T("Palette_UnsavedMessage"), PrimaryButtonText = T("Action_Discard"), CloseButtonText = T("Action_Cancel") }; return await AppDialogService.Default.ShowAsync(dialog) == ContentDialogResult.Primary; }
    private string CategoryName(ColorPaletteScheme scheme) => scheme.Category == ColorPaletteCategories.Custom ? scheme.CustomCategoryName ?? T("Palette_CategoryCustom") : CategoryBox.Items.OfType<Choice>().First(x => x.Id == scheme.Category).Name;
    private static Color ParseColor(string hex) => ColorPaletteStorageService.TryNormalizeHex(hex, out var normalized) ? ColorHelper.FromArgb(255, Convert.ToByte(normalized[1..3], 16), Convert.ToByte(normalized[3..5], 16), Convert.ToByte(normalized[5..7], 16)) : Colors.Gray;
    private Border CreateCardPreview(ColorPaletteScheme scheme)
    {
        var preview = new Border { Width = 88, Height = 68, Background = new SolidColorBrush(ParseColor(scheme.Colors.FirstOrDefault()?.Hex ?? "#808080")), CornerRadius = new CornerRadius(6), Margin = new Thickness(0, 0, 12, 0) };
        var first = scheme.Images.OrderBy(image => image.SortOrder).FirstOrDefault();
        if (first is not null)
        {
            try { var path = _storage.ResolveManagedImagePath(first.RelativePath); if (File.Exists(path)) preview.Child = new Image { Source = new BitmapImage(new Uri(path)), Stretch = Stretch.UniformToFill }; } catch (ArgumentException) { }
        }
        return preview;
    }
    private void DiscardDraft() { foreach (var image in _newImages) _storage.DeleteManagedImage(image); _newImages.Clear(); _pendingImageDeletes.Clear(); }
    private void Show(InfoBar bar, string message) { bar.Message = message; bar.IsOpen = true; }
    private sealed record Choice(string Id, string Name) { public override string ToString() => Name; }
}
