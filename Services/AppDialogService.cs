using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace UrbanPlanToolbox.Services;

public interface IAppDialogService
{
    Task<ContentDialogResult> ShowAsync(ContentDialog dialog, CancellationToken cancellationToken = default);
}

/// <summary>Serializes ContentDialog display per application window and rejects stale requests.</summary>
public sealed class AppDialogService : IAppDialogService
{
    private readonly SemaphoreSlim _queue = new(1, 1);
    public static IAppDialogService Default { get; } = new AppDialogService();

    public async Task<ContentDialogResult> ShowAsync(ContentDialog dialog, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        if (dialog.XamlRoot is null || cancellationToken.IsCancellationRequested) return ContentDialogResult.None;
        try
        {
            await _queue.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return ContentDialogResult.None;
        }
        try
        {
            if (dialog.XamlRoot is null || cancellationToken.IsCancellationRequested) return ContentDialogResult.None;
            if (dialog.XamlRoot.Content is FrameworkElement root)
            {
                dialog.RequestedTheme = root.ActualTheme;
                var themeKey = new Windows.UI.ViewManagement.AccessibilitySettings().HighContrast
                    ? "HighContrast"
                    : root.ActualTheme == ElementTheme.Dark ? "Dark" : "Light";
                var themeResources = Application.Current.Resources.ThemeDictionaries[themeKey] as ResourceDictionary;
                dialog.Background = themeResources?["AppTransientSurfaceBrush"] as Microsoft.UI.Xaml.Media.Brush;
                dialog.BorderBrush = themeResources?["AppTransientSurfaceBorderBrush"] as Microsoft.UI.Xaml.Media.Brush;
            }
            return await dialog.ShowAsync();
        }
        catch (InvalidOperationException)
        {
            // A closing window can invalidate the XamlRoot between the checks above.
            return ContentDialogResult.None;
        }
        finally
        {
            _queue.Release();
        }
    }
}
