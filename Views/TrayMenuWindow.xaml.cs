using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Services;
using Windows.Graphics;

namespace UrbanPlanToolbox.Views;

/// <summary>Compact WinUI 3 context-menu surface shown from the notification-area icon.</summary>
public sealed partial class TrayMenuWindow : Window
{
    private const double MenuWidthDip = 220;
    private const double MenuHeightDip = 154;

    private bool _allowClose;
    private bool _isVisible;

    public event EventHandler? OpenRequested;
    public event EventHandler? RecorderRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? ExitRequested;

    public TrayMenuWindow()
    {
        InitializeComponent();
        AppWindow.IsShownInSwitchers = false;
        AppWindow.SetPresenter(OverlappedPresenter.CreateForContextMenu());
        AppWindow.Closing += OnClosing;
        Activated += OnActivated;
        LocalizationService.Default.LanguageChanged += OnLanguageChanged;
        SettingsService.SettingsChanged += OnSettingsChanged;
        RefreshLocalizedStrings();
        ApplyTheme(new SettingsService().Load().Theme);
    }

    public void ShowAt(PointInt32 cursorPosition, bool recorderVisible)
    {
        RecorderCheck.Visibility = recorderVisible ? Visibility.Visible : Visibility.Collapsed;

        var size = GetPhysicalMenuSize();
        AppWindow.Resize(size);
        AppWindow.Move(CalculatePopupPosition(cursorPosition, size));
        AppWindow.Show();
        Activate();
        _isVisible = true;
    }

    public void HideMenu()
    {
        if (!_isVisible) return;
        AppWindow.Hide();
        _isVisible = false;
    }

    public void CloseForExit()
    {
        _allowClose = true;
        LocalizationService.Default.LanguageChanged -= OnLanguageChanged;
        SettingsService.SettingsChanged -= OnSettingsChanged;
        Close();
    }

    private SizeInt32 GetPhysicalMenuSize()
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var dpi = GetDpiForWindow(hwnd);
        var scale = dpi == 0 ? 1d : dpi / 96d;
        return new SizeInt32(
            Math.Max(1, (int)Math.Round(MenuWidthDip * scale)),
            Math.Max(1, (int)Math.Round(MenuHeightDip * scale)));
    }

    private static PointInt32 CalculatePopupPosition(PointInt32 cursor, SizeInt32 size)
    {
        var display = DisplayArea.GetFromPoint(cursor, DisplayAreaFallback.Primary);
        var workArea = display.WorkArea;
        var right = workArea.X + workArea.Width;
        var bottom = workArea.Y + workArea.Height;

        var x = cursor.X + size.Width <= right ? cursor.X : cursor.X - size.Width;
        var y = cursor.Y + size.Height <= bottom ? cursor.Y : cursor.Y - size.Height;

        x = Math.Clamp(x, workArea.X, Math.Max(workArea.X, right - size.Width));
        y = Math.Clamp(y, workArea.Y, Math.Max(workArea.Y, bottom - size.Height));
        return new PointInt32(x, y);
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        if (_isVisible && args.WindowActivationState == WindowActivationState.Deactivated)
            HideMenu();
    }

    private void OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_allowClose) return;
        args.Cancel = true;
        HideMenu();
    }

    private void OnLanguageChanged(object? sender, LanguageChangedEventArgs args) =>
        DispatcherQueue.TryEnqueue(RefreshLocalizedStrings);

    private void OnSettingsChanged(object? sender, AppSettings settings) =>
        DispatcherQueue.TryEnqueue(() => ApplyTheme(settings.Theme));

    private void ApplyTheme(string? theme) => ThemePreference.Apply(RootLayout, theme);

    private void RefreshLocalizedStrings()
    {
        var localization = LocalizationService.Default;
        SetMenuText(OpenButton, OpenText, localization.GetString("Tray_Open"));
        SetMenuText(RecorderButton, RecorderText, localization.GetString("Tray_Recorder"));
        SetMenuText(SettingsButton, SettingsText, localization.GetString("Tray_Settings"));
        SetMenuText(ExitButton, ExitText, localization.GetString("Tray_Exit"));
    }

    private static void SetMenuText(Button button, TextBlock textBlock, string text)
    {
        textBlock.Text = text;
        AutomationProperties.SetName(button, text);
    }

    private void InvokeAndHide(EventHandler? handler)
    {
        HideMenu();
        handler?.Invoke(this, EventArgs.Empty);
    }

    private void OnOpen(object sender, RoutedEventArgs e) => InvokeAndHide(OpenRequested);
    private void OnRecorder(object sender, RoutedEventArgs e) => InvokeAndHide(RecorderRequested);
    private void OnSettings(object sender, RoutedEventArgs e) => InvokeAndHide(SettingsRequested);
    private void OnExit(object sender, RoutedEventArgs e) => InvokeAndHide(ExitRequested);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hwnd);
}