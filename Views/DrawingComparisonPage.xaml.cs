using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using OpenCvSharp;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using WinRT.Interop;
using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Services;
using UrbanPlanToolbox.ViewModels;
using IOPath = System.IO.Path;

namespace UrbanPlanToolbox.Views;

public sealed partial class DrawingComparisonPage : Page
{
    private readonly DrawingComparisonViewModel _vm = new();
    private readonly DrawingLoadService _loader = new();
    private readonly DrawingComparisonService _comparison = new();
    private CancellationTokenSource? _cts;
    private Mat? _a;
    private Mat? _b;
    private string? _pathA;
    private string? _pathB;

    public DrawingComparisonPage()
    {
        try { InitializeComponent(); }
        catch (Exception exception) { AppLogger.Default.Error(nameof(DrawingComparisonPage), "XamlInitializationFailed", exception, exception.Message); throw; }
        TitleText.Text = T("Tool_DrawingVersionComparator_Name"); DescriptionText.Text = T("Tool_DrawingVersionComparator_Description"); AFileLabel.Text = T("Drawing_ImageA"); BFileLabel.Text = T("Drawing_ImageB"); PickAButton.Content = T("Drawing_PickA"); PickBButton.Content = T("Drawing_PickB"); CompareButton.Content = T("Drawing_Compare"); FitButton.Content = T("Drawing_Fit"); CancelButton.Content = T("Action_Cancel");
        OpacityLabel.Text = T("Drawing_Opacity"); WipeLabel.Text = T("Drawing_WipePosition"); ExportOverlayButton.Content = T("Drawing_ExportOverlay");
        foreach (var mode in Enum.GetValues<DrawingViewMode>()) ModeBox.Items.Add(new Choice(mode, T($"Drawing_Mode_{mode}")));
        ModeBox.SelectedIndex = 0;
        OpacitySlider.Value = 50; WipeSlider.Value = 50;
        SetStatus(T("Drawing_SelectBoth")); Unloaded += (_, _) => { _cts?.Cancel(); DisposeImages(); };
    }

    private string T(string key) => LocalizationService.Default.GetString(key);
    private async void OnPickA(object sender, RoutedEventArgs e) => await PickAsync(true);
    private async void OnPickB(object sender, RoutedEventArgs e) => await PickAsync(false);
    private async Task PickAsync(bool isA)
    {
        if (App.MainWindow is null) return;
        var picker = new FileOpenPicker(); foreach (var extension in new[] { ".png", ".jpg", ".jpeg", ".pdf" }) picker.FileTypeFilter.Add(extension); InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow)); var file = await picker.PickSingleFileAsync(); if (file is null) return;
        if (isA) { _pathA = file.Path; PickAButton.Content = IOPath.GetFileName(file.Path); AFileText.Text = file.Path; } else { _pathB = file.Path; PickBButton.Content = IOPath.GetFileName(file.Path); BFileText.Text = file.Path; }
    }
    private async void OnCompare(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_pathA) || string.IsNullOrWhiteSpace(_pathB)) { SetStatus(T("Drawing_SelectBoth")); return; }
        _cts?.Cancel(); _cts = new(); CompareButton.IsEnabled = false; CancelButton.IsEnabled = true; SetStatus(T("Drawing_Processing")); DisposeImages();
        try
        {
            _a = await _loader.LoadAsync(new(_pathA), _cts.Token); _b = await _loader.LoadAsync(new(_pathB), _cts.Token); var size = _comparison.ValidateSameSize(_a, _b);
            if (!size.IsValid) { SetStatus(T("Drawing_SizeMismatch")); DisposeImages(); return; }
            await RenderInputsAsync(_cts.Token); UpdateMode(); DispatcherQueue.TryEnqueue(FitAllViews); SetStatus(T("Drawing_Ready"));
        }
        catch (OperationCanceledException) { SetStatus(T("Drawing_Cancelled")); }
        catch (Exception exception) { AppLogger.Default.Error(nameof(DrawingComparisonPage), "CompareFailed", exception); SetStatus(T("Drawing_ProcessingFailed")); }
        finally { CompareButton.IsEnabled = true; CancelButton.IsEnabled = false; }
    }
    private async Task RenderInputsAsync(CancellationToken token)
    {
        var encoded = await Task.Run(() =>
        {
            using var previewA = CreateDisplayPreview(_a!); using var previewB = CreateDisplayPreview(_b!);
            return (DrawingLoadService.EncodePng(previewA), DrawingLoadService.EncodePng(previewB), previewA.Width, previewA.Height);
        }, token);
        token.ThrowIfCancellationRequested(); var a = ToBitmap(encoded.Item1); var b = ToBitmap(encoded.Item2);
        CanvasA.Source = a; CanvasA.Width = encoded.Width; CanvasA.Height = encoded.Height; foreach (var image in new[] { OverlayB, WipeB }) { image.Source = b; image.Width = encoded.Width; image.Height = encoded.Height; } CanvasSurface.Width = encoded.Width; CanvasSurface.Height = encoded.Height; UpdateWipe();
    }
    private void OnCancel(object sender, RoutedEventArgs e) => _cts?.Cancel();
    private void OnModeChanged(object sender, SelectionChangedEventArgs e) { if (ModeBox.SelectedItem is Choice choice) _vm.ViewMode = (DrawingViewMode)choice.Value; UpdateMode(); }
    private void UpdateMode()
    {
        var mode = _vm.ViewMode; OverlayControls.Visibility = mode == DrawingViewMode.Overlay ? Visibility.Visible : Visibility.Collapsed; WipeControls.Visibility = mode == DrawingViewMode.Wipe ? Visibility.Visible : Visibility.Collapsed;
        CanvasA.Visibility = Visibility.Visible; OverlayB.Visibility = mode == DrawingViewMode.Overlay ? Visibility.Visible : Visibility.Collapsed; WipeB.Visibility = mode == DrawingViewMode.Wipe ? Visibility.Visible : Visibility.Collapsed; WipeDivider.Visibility = mode == DrawingViewMode.Wipe ? Visibility.Visible : Visibility.Collapsed; if (mode == DrawingViewMode.Overlay) UpdateOverlay(); if (mode == DrawingViewMode.Wipe) UpdateWipe();
    }
    private void OnOverlayChanged(object sender, RangeBaseValueChangedEventArgs e) => UpdateOverlay();
    private void UpdateOverlay() { OpacityValue.Text = $"{OpacitySlider.Value:0}%"; OverlayB.Opacity = OpacitySlider.Value / 100d; }
    private void OnWipeChanged(object sender, RangeBaseValueChangedEventArgs e) => UpdateWipe();
    private void OnCanvasSizeChanged(object sender, SizeChangedEventArgs e) => UpdateWipe();
    private void OnWipePointer(object sender, PointerRoutedEventArgs e) { if (_vm.ViewMode != DrawingViewMode.Wipe || !e.GetCurrentPoint(CanvasSurface).Properties.IsLeftButtonPressed && e.Pointer.PointerDeviceType != Microsoft.UI.Input.PointerDeviceType.Mouse) return; WipeSlider.Value = Math.Clamp(e.GetCurrentPoint(CanvasSurface).Position.X / Math.Max(1, CanvasSurface.ActualWidth) * 100, 0, 100); e.Handled = true; }
    private void UpdateWipe() { var width = CanvasSurface.ActualWidth; var height = CanvasSurface.ActualHeight; var x = width * WipeSlider.Value / 100; WipeClip.Rect = new Windows.Foundation.Rect(x, 0, Math.Max(0, width - x), height); WipeDivider.Margin = new Thickness(x, 0, 0, 0); }
    private void OnViewerWheel(object sender, PointerRoutedEventArgs e) { if (sender is not ScrollViewer viewer) return; var factor = e.GetCurrentPoint(viewer).Properties.MouseWheelDelta > 0 ? 1.15f : .87f; viewer.ChangeView(null, null, Math.Clamp(viewer.ZoomFactor * factor, .25f, 4f)); e.Handled = true; }
    private void OnFit(object sender, RoutedEventArgs e) => FitAllViews();
    private void OnFitDoubleTapped(object sender, DoubleTappedRoutedEventArgs e) => OnFit(sender, e);
    private void FitAllViews()
    {
        if (CanvasSurface.Width <= 0 || CanvasSurface.Height <= 0) return;
        FitViewer(MainViewer, CanvasSurface.Width, CanvasSurface.Height);
    }
    private static void FitViewer(ScrollViewer viewer, double imageWidth, double imageHeight)
    {
        if (viewer.ViewportWidth <= 0 || viewer.ViewportHeight <= 0) return;
        var scale = Math.Clamp(Math.Min(viewer.ViewportWidth / imageWidth, viewer.ViewportHeight / imageHeight), viewer.MinZoomFactor, 1d);
        viewer.ChangeView(0, 0, (float)scale);
    }
    private static Mat CreateDisplayPreview(Mat source)
    {
        const int maximumDisplayDimension = 1600;
        var largestDimension = Math.Max(source.Width, source.Height);
        if (largestDimension <= maximumDisplayDimension) return source.Clone();
        var scale = maximumDisplayDimension / (double)largestDimension;
        var preview = new Mat(); Cv2.Resize(source, preview, new OpenCvSharp.Size(Math.Max(1, (int)Math.Round(source.Width * scale)), Math.Max(1, (int)Math.Round(source.Height * scale))), 0, 0, InterpolationFlags.Area); return preview;
    }
    private async void OnExportOverlay(object sender, RoutedEventArgs e) { if (_a is null || _b is null) return; using var overlay = _comparison.CreateOverlay(_a, _b, OpacitySlider.Value / 100d); await ExportAsync(DrawingLoadService.EncodePng(overlay), "overlay.png"); }
    private async Task ExportAsync(byte[]? png, string suggestedName) { if (png is null || App.MainWindow is null) return; var picker = new FileSavePicker(); picker.FileTypeChoices.Add("PNG", new[] { ".png" }); picker.SuggestedFileName = suggestedName; InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow)); var file = await picker.PickSaveFileAsync(); if (file is null) return; await File.WriteAllBytesAsync(file.Path, png); SetStatus(T("Drawing_Exported")); }
    private static BitmapImage ToBitmap(Mat? image) => image is null ? new BitmapImage() : ToBitmap(DrawingLoadService.EncodePng(image));
    private static BitmapImage ToBitmap(byte[] png) { var image = new BitmapImage(); using var stream = new InMemoryRandomAccessStream(); stream.WriteAsync(png.AsBuffer()).AsTask().GetAwaiter().GetResult(); stream.Seek(0); image.SetSource(stream); return image; }
    private void SetStatus(string value) { _vm.Status = value; StatusBar.Message = value; }
    private void DisposeImages() { _a?.Dispose(); _b?.Dispose(); _a = _b = null; }
    private sealed record Choice(object Value, string Name) { public override string ToString() => Name; }
}
